// Copyright (c) homehttp.com.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

abstract class ResponseReader
{
	internal Socket _sock;
	internal byte[] _buffer;
	internal int _bufferindex;
	internal int _bufferlength;

	protected int ReadFromBuffer(byte[] buffer, int index, int length)
	{
		int rc;
		if (_bufferlength > 0)
		{
			int len = Math.Min(_bufferlength, length);
			Buffer.BlockCopy(_buffer, _bufferindex, buffer, index, len);
			_bufferlength -= len;
			if (_bufferlength == 0)
				_bufferindex = 0;
			else
				_bufferindex += len;
			rc = len;
		}
		else
		{
			rc = _sock.Receive(buffer, index, length, SocketFlags.None);
		}
		return rc;
	}


	public abstract int Read(byte[] buffer, int index, int length);

	internal bool _completed;

	protected void OnReadComplete(EventArgs args)
	{
		_completed = true;
	}


	static public int IndexOfCRLF(byte[] bytes, int index, int len)
	{
		int endpos = index + len;
		for (; index + 2 <= endpos; index++)
		{
			if (bytes[index] == 13 && bytes[index + 1] == 10)
				return index;
		}

		return -1;
	}
}

class ContentReader : ResponseReader
{
	long _readlength = 0;
	long _contentlength;

	public ContentReader(long cntlen)
	{
		_contentlength = cntlen;
		if(cntlen<=0)
			_completed = true;
	}

	public override int Read(byte[] buffer, int index, int length)
	{
		if (_contentlength == 0)
		{
			OnReadComplete(EventArgs.Empty);
			return 0;
		}

		if (_contentlength == -1)
		{
			int rc = ReadFromBuffer(buffer, index, length);
			if (rc == 0)
			{
				_contentlength = _readlength;
				OnReadComplete(EventArgs.Empty);
				return 0;
			}
			_readlength += rc;
			return rc;
		}
		else
		{
			long restlen = _contentlength - _readlength;
			if (restlen <= 0)
				return 0;
			if (restlen < length) length = (int)restlen;
			if (length == 0)
				return 0;

			int rc = ReadFromBuffer(buffer, index, length);
			if (rc == 0) throw (new Exception("zerodata"));
			_readlength += rc;
			if (_contentlength < _readlength)
				throw (new Exception("Content-Length is smaller? " + _contentlength + "/" + _readlength));
			if (_contentlength == _readlength)
				OnReadComplete(EventArgs.Empty);
			return rc;
		}

	}

}
class ChunkedReader : ResponseReader
{

	int chunksize = -1;
	int outputsize = 0;

	public override int Read(byte[] buffer, int index, int length)
	{
		if (chunksize == 0)
			return 0;

	RETRY:

		int restsize = chunksize - outputsize;
		if (restsize > 0)
		{
			int rc = ReadFromBuffer(buffer, index, Math.Min(restsize, length));
			if (rc == 0) throw (new Exception("zerodata"));
			outputsize += rc;
			return rc;
		}

		byte[] searchbuff = new byte[1024];
		int searchpos = 0;
		int searchlen = 0;
		int nextsize = 0;

		while (true)
		{
			int crlfpos = -1;
			if (searchlen > 0)
				crlfpos = IndexOfCRLF(searchbuff, searchpos, searchlen - searchpos);
			if (crlfpos == -1)
			{
				int rl = searchbuff.Length - searchlen;
				if (rl == 0)
					throw (new Exception("CRLF not found in buffer size : " + searchlen));
				int rc = ReadFromBuffer(searchbuff, searchlen, rl);
				if (rc == 0) throw (new Exception("zerodata"));
				searchlen += rc;
				continue;
			}

			if (crlfpos - searchpos > 12)
				throw (new Exception("CRLF wrong position"));
			string chunkstr = Encoding.UTF8.GetString(searchbuff, searchpos, crlfpos - searchpos).Trim();

			searchpos = crlfpos + 2;

			if (chunkstr.Length == 0)
				continue;
			if (!int.TryParse(chunkstr, System.Globalization.NumberStyles.HexNumber, null, out nextsize))
				throw (new Exception("invalid chunk size"));
			if (nextsize < 0)
				throw (new Exception("invalid chunk size"));
			break;
		}

		chunksize = nextsize;
		outputsize = 0;
		if (chunksize == 0)
		{
			//TODO:ensure the last block received
			OnReadComplete(EventArgs.Empty);
			return 0;
		}

		int bufflen = searchlen - searchpos;
		if (bufflen > 0)
		{
			if (_bufferlength == 0)
			{
				Buffer.BlockCopy(searchbuff, searchpos, _buffer, 0, bufflen);
				_bufferindex = 0;
				_bufferlength = bufflen;
			}
			else if (bufflen <= _bufferindex)
			{
				_bufferindex -= bufflen;
				_bufferlength += bufflen;
				Buffer.BlockCopy(searchbuff, searchpos, _buffer, _bufferindex, bufflen);
			}
			else
			{
				byte[] newbuffer = new byte[bufflen + _bufferlength];
				Buffer.BlockCopy(searchbuff, searchpos, newbuffer, 0, bufflen);
				Buffer.BlockCopy(_buffer, _bufferindex, newbuffer, bufflen, _bufferlength);
				_buffer = newbuffer;
				_bufferindex = 0;
				_bufferlength = _buffer.Length;
			}
		}
		goto RETRY;
	}
}
