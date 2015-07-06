using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Abstractions.Replication;
using Raven.Client.Document;

namespace ReplicationDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            //
            using (var store = new DocumentStore
            {
                Url = "http://localhost:8080",
                DefaultDatabase = "DatabaseB"
            })
            {
                //this is default setting
                store.Conventions.FailoverBehavior = FailoverBehavior.AllowReadsFromSecondariesAndWritesToSecondaries | FailoverBehavior.ReadFromAllServers;                

                store.Initialize();
                var finishFetchingEvent = new ManualResetEventSlim();

                Task.Run(() =>
                {
                    do
                    {
                        try
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            using (var session = store.OpenSession())
                            {
                                var timer = Stopwatch.StartNew();
                                var order = session.Load<dynamic>("users/1");
                                timer.Stop();
                                Console.WriteLine("fetched name, Name = {0}, latency = {1}ms", order.Name,
                                    timer.ElapsedMilliseconds);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            throw;
                        }
                    } while (!finishFetchingEvent.Wait(TimeSpan.FromMilliseconds(250)));
                });

                Console.ReadKey();
                finishFetchingEvent.Set();
            }
        }
    }
}
