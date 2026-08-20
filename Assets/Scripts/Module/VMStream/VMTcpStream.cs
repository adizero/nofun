/*
 * (C) 2026 Radrat Softworks
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Net.Sockets;

namespace Nofun.Module.VMStream
{
    /// <summary>
    /// TCP network stream. The stream name is "host:port".
    /// </summary>
    public class VMTcpStream : IVMHostStream
    {
        private readonly TcpClient client;

        public static IVMHostStream Create(string address, uint mode)
        {
            int portSeparator = address.LastIndexOf(':');
            if (portSeparator < 0)
            {
                throw new ArgumentException($"TCP stream address must be in host:port form, got: {address}");
            }

            string host = address.Substring(0, portSeparator);
            int port = int.Parse(address.Substring(portSeparator + 1));

            TcpClient client = new TcpClient();
            client.Connect(host, port);

            return new VMTcpStream(client);
        }

        private VMTcpStream(TcpClient client)
        {
            this.client = client;
        }

        public int Read(Span<byte> buffer, object extraArgs)
        {
            try
            {
                // Non-blocking semantics: the game polls with vStreamReady.
                // A closed connection reports readable and then reads 0 (EOF).
                if (client.Available == 0)
                {
                    return 0;
                }

                return client.Client.Receive(buffer, SocketFlags.None);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public int Write(Span<byte> buffer, object extraArgs)
        {
            try
            {
                return client.Client.Send(buffer, SocketFlags.None);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public int Seek(int offset, StreamSeekMode whence)
        {
            return -1;
        }

        public int Tell()
        {
            return -1;
        }

        public int Ready()
        {
            int flags = 0;

            try
            {
                // Readable with no buffered data means the connection closed;
                // still report readable so the game sees the 0-byte read (EOF)
                if ((client.Available > 0) || client.Client.Poll(0, SelectMode.SelectRead))
                {
                    flags |= (int)StreamFlags.Read;
                }

                if (client.Connected)
                {
                    flags |= (int)StreamFlags.Write;
                }
            }
            catch (Exception)
            {
            }

            return flags;
        }

        public void OnClose()
        {
            client.Close();
        }
    }
}
