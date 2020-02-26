﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Specialized;

namespace AutoDefrost
{
    class HttpServer : IDisposable
    {
        private HttpListener httpListener;
        private Thread listenerLoop;
        private Thread[] requestProcessors;
        private BlockingCollection<HttpListenerContext> messages;
        private object m_lock;
        public float dpm_dewpoint; 
        public float dpm_airtemp; 
        public float dpm_hr; 
        public string dpm_sn; 
        public DateTime dpm_last_update;

        public HttpServer(int threadCount)
        {
            requestProcessors = new Thread[threadCount];
            messages = new BlockingCollection<HttpListenerContext>();
            httpListener = new HttpListener();
        }

        public virtual int Port { get; set; } = 80;

        public virtual string[] Prefixes
        {
            get { return new string[] { string.Format(@"http://+:{0}/", Port) }; }

        }

        public void Start()
        {
            listenerLoop = new Thread(HandleRequests);
            listenerLoop.IsBackground = true;

            foreach (string prefix in Prefixes) httpListener.Prefixes.Add(prefix);

            listenerLoop.Start();

            for (int i = 0; i < requestProcessors.Length; i++)
            {
                requestProcessors[i] = StartProcessor(i, messages);
            }
        }

        public void Dispose() { Stop(); }

        public void Stop()
        {
            messages.CompleteAdding();

            foreach (Thread worker in requestProcessors) worker.Join();

            httpListener.Stop();
            listenerLoop.Join();
        }

        private void HandleRequests()
        {
            httpListener.Start();
            //  netsh http add urlacl url="http://+:8085/" user=everyone
            try
            {
                while (httpListener.IsListening)
                {
                    Console.WriteLine("The listener is listening!");
                    HttpListenerContext context = httpListener.GetContext();

                    messages.Add(context);
                    Console.WriteLine("The listener has added a message!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private Thread StartProcessor(int number, BlockingCollection<HttpListenerContext> messages)
        {
            Thread thread = new Thread(() => Processor(number, messages));
            thread.IsBackground = true;
            thread.Start();
            return thread;
        }

        private void Processor(int number, BlockingCollection<HttpListenerContext> messages)
        {
            Console.WriteLine("Processor {0} started.", number);
            try
            {
                for (; ; )
                {
                    Console.WriteLine("Processor {0} awoken.", number);
                    HttpListenerContext context = messages.Take();
                    Console.WriteLine("Processor {0} dequeued message.", number);
                    Response(context);
                    Console.WriteLine("dewpoint: " + context.Request.QueryString["dewpoint"]);

                    if (context.Request.QueryString.Count >0)
                    {
                        //UpdateValues(context.Request.QueryString);
                            if (context.Request.QueryString["dewpoint"] != null) { Console.WriteLine("woo got DP"); dpm_dewpoint = float.Parse(context.Request.QueryString["dewpoint"]); }
                            if (context.Request.QueryString["airtemp"] != null) { Console.WriteLine("woo got airtemp"); dpm_airtemp = float.Parse(context.Request.QueryString["airtemp"]); }
                            if (context.Request.QueryString["rh"] != null) { Console.WriteLine("woo got rh"); dpm_hr = float.Parse(context.Request.QueryString["rh"]); }
                            if (context.Request.QueryString["sn"] != null) { Console.WriteLine("woo got sn"); dpm_sn = context.Request.QueryString["sn"]; }
                            dpm_last_update = DateTime.Now;
                    }
                }
            }
            catch { }

            Console.WriteLine("Processor {0} terminated.", number);
        }

        private void UpdateValues(NameValueCollection queryString)
        {
            
            lock(m_lock) {
                //if ( queryString["dewpoint"] != null) { Console.WriteLine("woo got DP"); dpm_dewpoint =  float.Parse(queryString["dewpoint"]); }
                //if (queryString["airtemp"] != null) { Console.WriteLine("woo got airtemp"); dpm_airtemp = float.Parse(queryString["airtemp"]); }
                //if (queryString["rh"] != null) { Console.WriteLine("woo got rh"); dpm_hr = float.Parse(queryString["rh"]); }
                //if (queryString["sn"] != null) { Console.WriteLine("woo got sn"); dpm_sn = queryString["sn"]; }
                //dpm_last_update = DateTime.Now;
                }

        }


        public virtual void Response(HttpListenerContext context)
        {
            SendReply(context, new StringBuilder("<html><head><title>NULL</title></head><body>Thanks!</body></html>"));
        }

        public static void SendReply(HttpListenerContext context, StringBuilder responseString)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString.ToString());
            context.Response.ContentLength64 = buffer.Length;
            System.IO.Stream output = context.Response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }
    }
}
