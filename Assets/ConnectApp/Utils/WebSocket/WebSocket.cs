using System;
using System.Security.Authentication;
using System.Text;
using UnityEngine;
using WebSocketSharp;

namespace ConnectApp.Utils {
    
    public enum WebSocketState
    {
        NotConnected,
        Connecting,
        Connected,
        Closed,
        Error
    }
    
    enum SslProtocolsHack {
        Tls = 192,
        Tls11 = 768,
        Tls12 = 3072
    }
    
    public class WebSocket {
        const bool EnableDebugLog = true;
        const bool EnableWebSocketSharpLog = false;
        
        const SslProtocols sslProtocolHack = (SslProtocols)(SslProtocolsHack.Tls12 | SslProtocolsHack.Tls11 | SslProtocolsHack.Tls);
        
        readonly WebSocketHost m_Host;
        readonly bool m_useSslHandShakeHack;
        
        WebSocketSharp.WebSocket m_Socket;
        WebSocketState m_State;

        
        public WebSocketState currentState {
            get { return this.m_State; }
        }

        public WebSocket(WebSocketHost host, bool useSslHandShakeHack)
        {
            this.m_State = WebSocketState.NotConnected;
            this.m_Host = host;
            this.m_useSslHandShakeHack = useSslHandShakeHack;
        }
      
        public bool checkSslProtocolHackFlag() {
            return this.m_Socket.SslConfiguration.EnabledSslProtocols != sslProtocolHack;
        }

        public void Connect(string url, Action OnConnected, Action<byte[]> OnMessage,
            Action<string> OnError, Action<int> OnClose)
        {
            DebugAssert(this.m_State == WebSocketState.NotConnected, $"fatal error: Cannot Connect to {url} because the socket is already set up.");
            this.m_State = WebSocketState.Connecting;
            
            this.m_Socket = new WebSocketSharp.WebSocket(url);
            if (this.m_useSslHandShakeHack) {
                this.m_Socket.SslConfiguration.EnabledSslProtocols = sslProtocolHack;
            }

            if (EnableWebSocketSharpLog) {
                this.m_Socket.Log.Level = LogLevel.Debug;
                this.m_Socket.Log.File = @"log.txt";
            }

            this.m_Socket.OnOpen += (sender, e) => {
                this.m_State = WebSocketState.Connected;
                this.m_Host.Enqueue(() => {
                    OnConnected?.Invoke();
                });
            };
            
            this.m_Socket.OnMessage += (sender, e) => {
                this.m_Host.Enqueue(() => {
                    OnMessage(e.RawData);
                });
            };

            this.m_Socket.OnError += (sender, e) => {
                this.m_Host.Enqueue(() => {
                    this.m_State = WebSocketState.Error;
                    OnError(e.Message);
                });
            };

            this.m_Socket.OnClose += (sender, e) => {
                this.m_Host.Enqueue(() => {
                    this.m_State = WebSocketState.Closed;
                    OnClose?.Invoke(e.Code);
                });
            };

            this.m_Socket.ConnectAsync();
        }
        
        public void Send(string content) {
            DebugAssert(this.m_State == WebSocketState.Connected, "fatal error: Cannot send data before connect!");
            DebugAssert(this.m_Socket != null, "fatal error: Cannot send data because the websocket is null.");
            
            var bytes = Encoding.UTF8.GetBytes (content);
            this.m_Socket.Send(bytes);
        }

        public void Close() {
            if (this.m_State == WebSocketState.Connected ||
                this.m_State == WebSocketState.Connecting) {
                this.m_Socket.CloseAsync();
            }   
                            
            this.m_State = WebSocketState.Closed;
        }

        static void DebugAssert(bool condition, string logMsg) {
            if (EnableDebugLog && !condition) {
                Debug.Log(logMsg);
            }
        }
    }
}