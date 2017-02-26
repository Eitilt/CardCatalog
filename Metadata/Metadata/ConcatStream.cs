using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metadata {
    /// <summary>
    /// Join multiple streams into a single continuous object.
    /// </summary>
    class ConcatStream : Stream {
        /// <summary>
        /// The underlying individual streams, before concatenation.
        /// </summary>
        List<Stream> streams;
        /// <summary>
        /// The index of the currently active individual <see cref="Stream"/>
        /// within <see cref="streams"/>.
        /// </summary>
        int index = 0;

        /// <summary>
        /// Progress to the next concatenated stream, seeking to its beginning
        /// if possible.
        /// </summary>
        /// 
        /// <returns>
        /// `false` there is no available next stream, `true` otherwise.
        /// </returns>
        private bool IncrementIndex() {
            if (index >= streams.Count)
                return false;

            ++index;
            if (index >= streams.Count)
                return false;

            if (streams[index].CanSeek)
                streams[index].Position = 0;

            return true;
        }

        /// <summary>
        /// Whether all concatenated streams (and therefore this wrapper)
        /// support reading.
        /// </summary>
        /// 
        /// <remarks>
        /// If any concatenated stream is closed, this property returns false.
        /// </remarks>
        public override bool CanRead =>
            streams.All((s) => s.CanRead);

        /// <summary>
        /// Whether all concatenated streams (and therefore this wrapper)
        /// support seeking.
        /// </summary>
        /// 
        /// <remarks>
        /// If any concatenated stream is closed, this property returns false.
        /// </remarks>
        public override bool CanSeek =>
            streams.All((s) => s.CanSeek);

        /// <summary>
        /// Whether any concatenated stream (and therefore this wrapper)
        /// may time out.
        /// </summary>
        public override bool CanTimeout =>
            streams.Any((s) => s.CanTimeout);

        /// <summary>
        /// Whether all concatenated streams (and therefore this wrapper)
        /// support writing.
        /// </summary>
        /// 
        /// <remarks>
        /// If any concatenated stream is closed, this property returns false.
        /// </remarks>
        public override bool CanWrite =>
            streams.All((s) => s.CanWrite);

        /// <summary>
        /// The length in bytes of the combined stream.
        /// </summary>
        /// 
        /// <exception cref="NotSupportedException">
        /// At least one concatenated <see cref="Stream"/> does not support
        /// seeking.
        /// </exception>
        public override long Length =>
            streams.Sum((s) => s.Length);

        /// <summary>
        /// Gets or sets the position of the concatenated stream.
        /// </summary>
        /// 
        /// <exception cref="IndexOutOfRangeException">
        /// All concatenated streams must support seeking to get or set the
        /// position. Use the <see cref="CanSeek"/> property to determine
        /// whether the stream supports seeking.
        /// <para/>
        /// Unlike other <see cref="Stream"/> implementations, seeking to any
        /// location beyond the length of the stream is <em>not</em> supported.
        /// 
        /// TODO: See about bringing that into compliance.
        /// <para />
        /// The Position property does not keep track of the number of bytes
        /// from the stream that have been consumed, skipped, or both.
        /// </exception>
       /// 
        /// <seealso cref="Seek"/>
        public override long Position {
            get {
                if (CanSeek == false)
                    throw new NotSupportedException("The stream does not support seeking");

                long sum = streams.Take(index).Sum((s) => s.Length);
                return (sum + streams[index].Position);
            }
            set {
                Seek(value, SeekOrigin.Begin);
            }
        }

        //TODO: Implement Read/WriteTimeout property and CopyToAsync method overrides handling substreams

        /// <summary>
        /// Clears all buffers for the concatenated streams and causes any
        /// buffered data to be written to the underlying device.
        /// </summary>
        /// 
        /// <exception cref="IOException">An I/O error occurs.</exception>
        /// 
        /// <seealso cref="FlushAsync"/>
        public override void Flush() =>
            streams.ForEach((s) => s.Flush());

        /// <summary>
        /// Asynchronously clears all buffers for the concatenated streams and
        /// causes any buffered data to be written to the underlying device.
        /// </summary>
        /// 
        /// <param name="cancellationToken">The token to monitor for
        /// cancellation requests. The default value is
        /// <see cref="CancellationToken.None"/>.
        /// </param>
        /// 
        /// <returns>
        /// A task that represents the asynchronous flush operation.
        /// </returns>
        /// 
        /// <exception cref="IOException">An I/O error occurs.</exception>
        /// 
        /// <seealso cref="Flush"/>
        public override Task FlushAsync(CancellationToken cancellationToken) =>
            Task.WhenAll(streams.Select((s) => s.FlushAsync(cancellationToken)));

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the
        /// position within the stream by the number of bytes read.
        /// </summary>
        /// 
        /// <remarks>
        /// Use the <see cref="CanRead"/> property to determine whether the
        /// current instance supports reading. Use the <see cref="ReadAsync"/>
        /// method to read asynchronously from the current stream.
        /// <para/>
        /// If streams concatenated before <see cref="Position"/> have more
        /// elements added to them, the output remains unaffected until and
        /// unless <see cref="Position"/> is seeked to before those new
        /// elements.
        /// <para/>
        /// The current position within the stream is advanced by the number
        /// of bytes read; however, if an exception occurs, the current
        /// position within the stream remains unchanged. The call will block
        /// until at least one byte of data can be read, in the event that no
        /// data is available -- it returns 0 only when there is no more data
        /// in the stream and no more is expected (such as a closed socket or
        /// end of file). Fewer bytes than requested may be returned even if
        /// the end of the stream has not been reached.
        /// 
        /// TODO: Ensure that the rewind-on-exception occurs properly.
        /// </remarks>
        /// 
        /// <param name="buffer">
        /// When this method returns, the buffer contains the specified byte
        /// array with the values between offset and (offset + count - 1)
        /// replaced by the bytes read from the current source.
        /// </param>
        /// <param name="offset">
        /// The zero-based byte offset in buffer at which to begin storing the
        /// data read from the current stream.
        /// </param>
        /// <param name="count">
        /// The maximum number of bytes to be read from the current stream.
        /// </param>
        /// 
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less
        /// than the number of bytes requested if that many bytes are not
        /// currently available, or zero (0) if the end of the stream has been
        /// reached.
        /// </returns>
        /// 
        /// <exception cref="ArgumentException">
        /// The sum of offset and count is larger than the buffer length.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="buffer"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is negative.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The stream does not support reading.
        /// </exception>
        /// 
        /// <seealso cref="ReadAsync"/>
        /// <seealso cref="ReadByte"/>
        public override int Read(byte[] buffer, int offset, int count) {
            // Perform the necessary sanity checks
            if (CanRead == false)
                throw new NotSupportedException("The stream does not support reading");
            if (buffer == null)
                throw new ArgumentNullException("Read buffer is null", "buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("Read offset is negative", "offset");
            if (count < 0)
                throw new ArgumentOutOfRangeException("Read byte count is negative", "count");
            if ((offset + count) > buffer.Length)
                throw new ArgumentException("The sum of offset and count to read is larger than the buffer length", "buffer");

            // If the index points beyond the last Stream, that last Stream
            // must have been closed
            if (index >= streams.Count)
                return 0;

            var active = streams[index];
            // Unseekable streams act much more simply if the end is reached
            // prematurely (e.g. in a network stream) since their total length
            // can't be checked
            if (active.CanSeek) {
                var remaining = (int)(active.Length - active.Position);

                // If there are enough bytes in the stream to fulfill `count`
                if (remaining >= count) {
                    return active.Read(buffer, offset, count);
                } else {
                    var bytesRead = active.Read(buffer, offset, remaining);

                    if (bytesRead == 0) {
                        streams.RemoveAt(index);
                        return Read(buffer, offset, count);
                    } else if (bytesRead != remaining) {
                        // Don't return bytes out-of-order if less were
                        // returned than expected
                        return bytesRead;
                    } else {
                        IncrementIndex();
                        return (bytesRead + Read(buffer, (offset + remaining), (count - remaining)));
                    }
                }
            } else {
                var bytesRead = active.Read(buffer, offset, count);

                // If a stream is closed (at least one byte is always returned
                // otherwise) we can remove it from the list to allow disposal
                if (bytesRead == 0) {
                    streams.RemoveAt(index);
                    return Read(buffer, offset, count);
                } else {
                    return bytesRead;
                }
            }
        }

        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream
        /// and advances the position within the stream by the number of bytes
        /// read.
        /// </summary>
        /// 
        /// <remarks>
        /// Use the <see cref="CanRead"/> property to determine whether the
        /// current instance supports reading. Use the <see cref="Read"/>
        /// method to read synchronously from the current stream.
        /// <para/>
        /// If streams concatenated before <see cref="Position"/> have more
        /// elements added to them, the output remains unaffected until and
        /// unless <see cref="Position"/> is seeked to before those new
        /// elements.
        /// <para/>
        /// The current position within the stream is advanced by the number
        /// of bytes read; however, if an exception occurs, the current
        /// position within the stream remains unchanged. The call will block
        /// until at least one byte of data can be read, in the event that no
        /// data is available -- it returns 0 only when there is no more data
        /// in the stream and no more is expected (such as a closed socket or
        /// end of file). Fewer bytes than requested may be returned even if
        /// the end of the stream has not been reached.
        /// 
        /// TODO: Ensure that the rewind-on-exception occurs properly.
        /// <para/>
        /// TODO: If any method could benefit from a test suite, it's this one
        /// TODO: Should also break into smaller functions
        /// </remarks>
        /// 
        /// <param name="buffer">
        /// When this method returns, the buffer contains the specified byte
        /// array with the values between offset and (offset + count - 1)
        /// replaced by the bytes read from the current source.
        /// </param>
        /// <param name="offset">
        /// The zero-based byte offset in buffer at which to begin storing the
        /// data read from the current stream.
        /// </param>
        /// <param name="count">
        /// The maximum number of bytes to be read from the current stream.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value
        /// is <see cref="CancellationToken.None"/>.
        /// </param>
        /// 
        /// <returns>
        /// A task that represents the asynchronous read operation. The value
        /// is the total number of bytes read into the buffer. This can be less
        /// than the number of bytes requested if that many bytes are not
        /// currently available, or zero (0) if the end of the stream has been
        /// reached.
        /// </returns>
        /// 
        /// <exception cref="ArgumentException">
        /// The sum of offset and count is larger than the buffer length.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="buffer"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is negative.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The stream is currently in use by a previous read operation.
        /// 
        /// TODO: Implement this.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The stream does not support reading.
        /// </exception>
        /// 
        /// <seealso cref="Read"/>
        /// <seealso cref="ReadByte"/>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            // Perform the necessary sanity checks
            if (CanRead == false)
                throw new NotSupportedException("The stream does not support reading");
            if (buffer == null)
                throw new ArgumentNullException("Read buffer is null", "buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("Read offset is negative", "offset");
            if (count < 0)
                throw new ArgumentOutOfRangeException("Read byte count is negative", "count");
            if ((offset + count) > buffer.Length)
                throw new ArgumentException("The sum of offset and count to read is larger than the buffer length", "buffer");

            int lastIndex = 0;
            long? remainder = count;
            bool firstStream = true, lastStream = false;
            var forwardStreams = streams.Skip(index)
                .Select((s) => {
                    if (s.CanRead)
                        return Tuple.Create(s, s.Length);
                    else
                        return Tuple.Create(s, -1L);
                }).TakeWhile((s, i) => {
                    if (lastStream) {
                        return false;
                    } else if (s.Item1.CanSeek == false) {
                        lastIndex = i;
                        remainder = null;
                        // Include this Stream but not the next one
                        lastStream = true;
                        return true;
                    } else {
                        // All seekable streams but the first should be read
                        // from their beginning, not the current position
                        long len = s.Item2;
                        if (firstStream) {
                            len -= s.Item1.Position;
                            firstStream = false;
                        } else {
                            s.Item1.Position = 0;
                        }

                        if (len >= remainder) {
                            lastIndex = i;
                            lastStream = true;
                        } else {
                            remainder -= len;
                        }

                        return true;
                    }
                });
            /* Reset the length on the last stream to only what is necessary;
             * do it in a new line to save the processing if the test is false
             */
            if (remainder.HasValue) {
                forwardStreams.Select((s, i) => {
                    if (i == lastIndex)
                        return Tuple.Create(s.Item1, remainder.Value);
                    else
                        return s;
                });
            }

            // Create temporary buffers to avoid thread collisions, then
            // populate them from the appropriate streams
            var buffers = forwardStreams.Select((s) =>
                new byte[s.Item2]
            ).ToArray();
            var tasks = forwardStreams.Select((s, i) =>
                s.Item1.ReadAsync(buffers[i], 0, (int)s.Item2, cancellationToken)
            );

            var read = await Task.WhenAll(tasks);

            bool end = false;
            int added = 0;
            var ret = new List<byte>(count);
            for (int i = 0; i < read.Length; ++i) {
                // Ensure no unused bytes are lost
                if (end) {
                    int active = (index + i + added);
                    if (streams[active].CanSeek) {
                        continue;
                    } else if (streams[active - 1].CanSeek && streams[active - 1].CanWrite) {
                        streams[active - 1].Seek(0, SeekOrigin.End);
                        streams[active - 1].SetLength(streams[active - 1].Length + read[i]);
                        await streams[active - 1].WriteAsync(buffers[i], 0, read[i]);
                    } else {
                        streams.Insert((int)(active), new MemoryStream(buffers[i], 0, read[i]));
                        ++added;
                    }
                // Concatenation needs explicit handling for streams that
                // returned fewer bytes than expected
                } else {
                    if (read[i] == buffers[i].Length) {
                        ret.AddRange(buffers[i]);
                    } else {
                        ret.AddRange(buffers[i].Take(read[i]));
                        end = true;
                        // Set the index to the Stream that returned less than
                        // requested and return any following bytes as above
                        index += i;
                    }
                }
            }
            // If we got every byte we wanted, set the index to the last
            // Stream used
            if (end == false)
                index += (read.Length - 1);

            ret.CopyTo(buffer, offset);
            return ret.Count;
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the
        /// stream by one byte, or returns -1 if at the end of the stream.
        /// </summary>
        /// 
        /// <remarks>
        /// Use the <see cref="CanRead"/> property to determine whether the
        /// current instance supports reading.
        /// </remarks>
        /// 
        /// <returns>
        /// The unsigned byte cast to an <see cref="Int32"/>, or -1 if at the
        /// end of the stream.
        /// </returns>
        /// 
        /// <exception cref="NotSupportedException">
        /// The stream does not support reading.
        /// </exception>
        /// 
        /// <seealso cref="Read"/>
        /// <seealso cref="ReadAsync"/>
        public override int ReadByte() {
            if (CanRead == false)
                throw new NotSupportedException("The stream does not support reading");

            int ret = streams[index].ReadByte();
            while (ret == -1) {
                if (IncrementIndex())
                    ret = streams[index].ReadByte();
                else
                    return -1;
            }

            return ret;
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// 
        /// <remarks>
        /// Use the CanSeek property to determine whether the current instance
        /// supports seeking.
        /// <para/>
        /// If <paramref name="offset"/> is negative, the new position
        /// precedes the position specified by <paramref name="origin"/> by
        /// the number of bytes specified by <paramref name="offset"/>. If
        /// <paramref name="origin"/> is zero (0), the new position is the
        /// position specified by <paramref name="origin"/>. If
        /// <paramref name="offset"/> is positive, the new position follows
        /// the position specified by <paramref name="origin"/> by the number
        /// of bytes specified by <paramref name="offset"/>.
        /// <para/>
        /// Unlike other <see cref="Stream"/> implementations, seeking to any
        /// location beyond the length of the stream is <em>not</em> supported.
        /// 
        /// TODO: See about bringing that into compliance.
        /// </remarks>
        /// 
        /// <param name="offset">
        /// A byte offset relative to the origin parameter.
        /// </param>
        /// <param name="origin">
        /// The reference point used to obtain the new position.
        /// </param>
        /// 
        /// <returns>The new position within the current stream.</returns>
        /// 
        /// <exception cref="IOException">An I/O error occurs.</exception>
        /// <exception cref="NotSupportedException">
        /// The stream does not support reading.
        /// </exception>
        public override long Seek(long offset, SeekOrigin origin) {
            if (CanSeek == false)
                throw new NotSupportedException("The stream does not support reading");

            if (origin == SeekOrigin.Begin) {
                index = 0;
            } else if (origin == SeekOrigin.End) {
                index = (streams.Count - 1);
                offset += streams[index].Length;
            } else {
                // If seeking forward, setting Position to 0 means that more
                // bytes are needed to reach the same target, while if seeking
                // backward, `offset` will be negative
                offset += streams[index].Position;
            }
            streams[index].Position = 0;

            while (offset >= streams[index].Length) {
                offset -= streams[index].Length;
                ++index;

                if (index >= streams.Count)
                    throw new IndexOutOfRangeException("ConcatStream does not support seeking beyond the end of the stream");
            }
            while (offset < 0) {
                //TODO: Verify that this is indeed the proper behaviour when
                // trying to seek before the beginning of the stream
                if (index == 0)
                    return 0;

                --index;
                offset += streams[index].Length;
            }

            streams[index].Position = offset;
            return Position;
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// 
        /// <remarks>
        /// If the specified value is less than the current length of the
        /// stream, the stream is truncated. If the specified value is larger
        /// than the current length of the stream, the stream is expanded. If
        /// the stream is expanded, the contents of the stream between the old
        /// and the new length are not defined.
        /// <para/>
        /// Use the <see cref="CanRead"/> property to determine whether the
        /// current stream supports writing, and the <see cref="CanSeek"/>
        /// property to determine whether seeking is supported.
        /// </remarks>
        /// 
        /// <param name="value">
        /// The desired length of the current stream in bytes.
        /// </param>
        /// 
        /// <exception cref="IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The stream does not support both writing and seeking.
        /// </exception>
        public override void SetLength(long value) {
            if ((CanRead == false) || (CanWrite == false))
                throw new NotSupportedException("The stream does not support both writing and seeking");

            long remainder = 0;
            streams = streams.TakeWhile((s) => {
                if (value < 0)
                    return false;

                remainder = value;
                value -= s.Length;
                return true;
            }).ToList();

            streams[streams.Count - 1].SetLength(remainder);
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the
        /// current position within this stream by the number of bytes written.
        /// </summary>
        /// 
        /// <remarks>
        /// Use the <see cref="CanWrite"/> property to determine whether the
        /// current instance supports writing. 
        /// <para/>
        /// If the write operation is successful, the position within the
        /// stream advances by the number of bytes written. If an exception
        /// occurs, the position within the stream remains unchanged.
        /// 
        /// TODO: Ensure this is properly respected.
        /// </remarks>
        /// 
        /// <param name="buffer">
        /// An array of bytes. This method copies <paramref name="count"/>
        /// bytes from <paramref name="buffer"/> to the current stream. 
        /// </param>
        /// <param name="offset">
        /// The zero-based byte offset in <paramref name="buffer"/> at which
        /// to begin copying bytes to the current stream.
        /// </param>
        /// <param name="count">
        /// The number of bytes to be written to the current stream.
        /// </param>
        /// 
        /// <exception cref="ArgumentException">
        /// The sum of offset and count is larger than the buffer length.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="buffer"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is negative.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The stream does not support reading.
        /// </exception>
        /// 
        /// <seealso cref="WriteAsync"/>
        /// <seealso cref="WriteByte"/>
        public override void Write(byte[] buffer, int offset, int count) {
            // Perform the necessary sanity checks
            if (CanRead == false)
                throw new NotSupportedException("The stream does not support writing");
            if (buffer == null)
                throw new ArgumentNullException("Read buffer is null", "buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("Read offset is negative", "offset");
            if (count < 0)
                throw new ArgumentOutOfRangeException("Read byte count is negative", "count");
            if ((offset + count) > buffer.Length)
                throw new ArgumentException("The sum of offset and count to write is larger than the buffer length", "buffer");

            if (index >= streams.Count) {
                --index;
                streams[index].Write(buffer, offset, count);
                return;
            }

            var active = streams[index];
            // Unseekable streams act much more simply if the end is reached
            // prematurely (e.g. in a network stream) since their total length
            // can't be checked
            if (active.CanSeek) {
                var remaining = (int)(active.Length - active.Position);

                if (remaining >= count) {
                    active.Write(buffer, offset, count);
                    return;
                } else {
                    active.Write(buffer, offset, remaining);

                    ++index;
                    if (active.CanSeek)
                        active.Position = 0;
                }
            } else {
                active.Write(buffer, offset, count);
            }
        }

        /// <summary>
        /// Asynchronously writes a sequence of bytes to the current stream,
        /// advances the current position within this stream by the number of
        /// bytes written, and monitors cancellation requests.
        /// </summary>
        /// 
        /// <remarks>
        /// Use the <see cref="CanWrite"/> property to determine whether the
        /// current instance supports writing. Use the <see cref="Write"/>
        /// method to read synchronously from the current stream.
        /// <para/>
        /// If the write operation is successful, the position within the
        /// stream advances by the number of bytes written. If an exception
        /// occurs, the position within the stream remains unchanged.
        /// 
        /// TODO: Ensure this is properly respected.
        /// </remarks>
        /// 
        /// <param name="buffer">
        /// An array of bytes. This method copies <paramref name="count"/>
        /// bytes from <paramref name="buffer"/> to the current stream. 
        /// </param>
        /// <param name="offset">
        /// The zero-based byte offset in <paramref name="buffer"/> at which
        /// to begin copying bytes to the current stream.
        /// </param>
        /// <param name="count">
        /// The number of bytes to be written to the current stream.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value
        /// is <see cref="CancellationToken.None"/>.
        /// </param>
        /// 
        /// <returns>
        /// A task that represents the asynchronous write operation.
        /// </returns>
        /// 
        /// <exception cref="ArgumentException">
        /// The sum of offset and count is larger than the buffer length.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="buffer"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is negative.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The stream is currently in use by a previous write operation.
        /// 
        /// TODO: Implement this.
        /// 
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The stream does not support reading.
        /// </exception>
        /// 
        /// <seealso cref="Write"/>
        /// <seealso cref="WriteByte"/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            // Perform the necessary sanity checks
            if (CanRead == false)
                throw new NotSupportedException("The stream does not support writing");
            if (buffer == null)
                throw new ArgumentNullException("Read buffer is null", "buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("Read offset is negative", "offset");
            if (count < 0)
                throw new ArgumentOutOfRangeException("Read byte count is negative", "count");
            if ((offset + count) > buffer.Length)
                throw new ArgumentException("The sum of offset and count to write is larger than the buffer length", "buffer");

            int last = (streams.Count - 1);

            bool firstStream = true;
            var tasks = new List<Task>();
            for ( ; (index < last) && (count > 0); ++index) {
                if (streams[index].CanSeek) {
                    if (firstStream == false)
                        streams[index].Position = 0;

                    var len = Math.Min((int)(streams[index].Length - streams[index].Position), count);
                    tasks.Add(streams[index].WriteAsync(buffer, offset, len, cancellationToken));

                    offset += len;
                    count -= len;
                } else {
                    tasks.Add(streams[index].WriteAsync(buffer, offset, count, cancellationToken));
                    count = 0;
                    break;
                }
            }

            if (count > 0)
                tasks.Add(streams[last].WriteAsync(buffer, offset, count, cancellationToken));

            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Writes a byte to the current position in the stream and advances
        /// the position within the stream by one byte.
        /// </summary>
        /// 
        /// <remarks>
        /// Use the <see cref="CanWrite"/> property to determine whether the
        /// current instance supports writing.
        /// </remarks>
        /// 
        /// <exception cref="IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The stream does not support writing.
        /// </exception>
        /// 
        /// <seealso cref="Write"/>
        /// <seealso cref="WriteAsync"/>
        public override void WriteByte(byte value) {
            if (CanWrite == false)
                throw new NotSupportedException("The stream does not support writing");

            if (index >= streams.Count) {
                index = (streams.Count - 1);

                if (streams[index].CanSeek)
                    streams[index].Seek(0, SeekOrigin.End);
            }

            streams[index].WriteByte(value);
        }
    }
}
