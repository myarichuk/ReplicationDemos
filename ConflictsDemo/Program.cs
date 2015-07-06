using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Abstractions.Replication;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Exceptions;
using Raven.Client.Indexes;
using Raven.Client.Listeners;

namespace ConflictsDemo
{
    class Program
    {
        public class LastModifiedConflictResolver : IDocumentConflictListener
        {
            public bool TryResolveConflict(string key, JsonDocument[] conflictedDocs, out JsonDocument resolvedDocument)
            {
                var latestLastModified = conflictedDocs.Max(x => x.LastModified);
                resolvedDocument = conflictedDocs.First(x => x.LastModified == latestLastModified);
                resolvedDocument.Metadata.Remove("@id");
                resolvedDocument.Metadata.Remove("@etag");
                return true;
            }
        }

        public class User
        {
            public string Name { get; set; }
        }

        static void Main(string[] args)
        {
            using (var storeA = new DocumentStore
            {
                Url = "http://localhost:8080",
                DefaultDatabase = "DatabaseA"
            })
            using (var storeB = new DocumentStore
            {
                Url = "http://localhost:8080",
                DefaultDatabase = "DatabaseB",
            })
            {
                //storeB.RegisterListener(new LastModifiedConflictResolver());
                storeA.Initialize();
                storeB.Initialize();

                storeA.DatabaseCommands.GlobalAdmin.DeleteDatabase("DatabaseA", true);
                storeA.DatabaseCommands.GlobalAdmin.DeleteDatabase("DatabaseB", true);
                CreateDatabaseWithReplication("DatabaseA", storeA);
                CreateDatabaseWithReplication("DatabaseB", storeB);

                using (var sessionA = storeA.OpenSession())
                using (var sessionB = storeB.OpenSession())
                {
                    sessionA.Store(new User
                    {
                        Name = "John"
                    });

                    sessionB.Store(new User
                    {
                        Name = "Jane"
                    });

                    sessionA.SaveChanges();
                    sessionB.SaveChanges();
                }

                SetupReplication(storeA,"DatabaseA", "DatabaseB", "http://localhost:8080");
                SetupReplication(storeB, "DatabaseB", "DatabaseA", "http://localhost:8080");

                Console.WriteLine("Conflict created.");
                Console.ReadLine();

                try
                {
                    using (var session = storeB.OpenSession())
                    {
                        session.Load<dynamic>("users/1");
                    }
                }
                catch (ConflictException e)
                {
                    Console.WriteLine(e);
                }

                Console.ReadLine();

                storeB.RegisterListener(new LastModifiedConflictResolver());
                using (var session = storeB.OpenSession())
                {
                    var user = session.Load<dynamic>("users/1");
                    Console.WriteLine(user.Name);
                }
            }
        }

        private static void CreateDatabaseWithReplication(string databaseName, IDocumentStore store)
        {
            store.DatabaseCommands.GlobalAdmin.CreateDatabase(new DatabaseDocument
            {
                Id = databaseName,
                Settings = new Dictionary<string, string>
                {
					{
						"Raven/ActiveBundles", "Replication"
					},
					{
						"Raven/DataDir", "~/" + databaseName
					}
				}
            });
            using (var newStore = new DocumentStore
            {
                Url = store.Url,
                DefaultDatabase = databaseName
            }.Initialize())
            {
                newStore.ExecuteIndex(new RavenDocumentsByEntityName());
            }
        }

        private static void SetupReplication(IDocumentStore source, string sourceDatabaseName, string destinationDatabaseName, string targetUrl)
        {
            using (var session = source.OpenSession(sourceDatabaseName))
            {
                session.Store(new ReplicationDocument
                {
                    ClientConfiguration = new ReplicationClientConfiguration
                    {
                        FailoverBehavior = FailoverBehavior.AllowReadsFromSecondariesAndWritesToSecondaries
                    },
                    Destinations = new List<ReplicationDestination>
                    {
						new ReplicationDestination
						{
							Url = targetUrl,
							Database = destinationDatabaseName
						}
					}
                });
                session.SaveChanges();
            }
        } 
    }
}
