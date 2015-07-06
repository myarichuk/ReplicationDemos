using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raven.Abstractions.Replication;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Shard;

namespace ShardingDemo
{
    public class Animal
    {
        public static readonly string[] SpeciesNames = {
			"Dog",
			"Cat"			
		};

        public string Name { get; set; }
        public int Weight { get; set; }
        public string Species { get; set; }
    }

    class Program
    {
        private static Dictionary<string, IDocumentStore> shards;
        private static ShardedDocumentStore shardedStore;
        static void Main(string[] args)
        {
            shards = new Dictionary<string, IDocumentStore>()
			{

				{
					"Dog", new DocumentStore()
					{
						Url = "http://localhost:8080",
						DefaultDatabase = "DogsZoo"
					}
				},
				{
					"Cat", new DocumentStore()
					{
						Url = "http://localhost:8080",
						DefaultDatabase = "CatsZoo"
					}
				},
				
			};

            using (shardedStore = new ShardedDocumentStore(
                new ShardStrategy(shards).ShardingOn<Animal>(x => x.Species))
            {
                Conventions = new DocumentConvention()
                {
                    FailoverBehavior = FailoverBehavior.AllowReadsFromSecondariesAndWritesToSecondaries
                }
            })
            {

                shardedStore.Initialize();

                var rand = new Random();
                var counter = 0;
                Console.WriteLine("Press any key to generate a new animal, press Esc to quit");
                ConsoleKey curKey = Console.ReadKey().Key;

                while (curKey != ConsoleKey.Escape)
                {
                    try
                    {
                        var localSP = Stopwatch.StartNew();
                        var species = Animal.SpeciesNames[rand.Next(0, Animal.SpeciesNames.Length)];

                        using (var session = shardedStore.OpenSession())
                        {
                            session.Store(new Animal()
                            {
                                Name = string.Format("{0} #{1}", species, counter),
                                Species = species
                            });
                            session.SaveChanges();
                        }

                        Console.WriteLine("Created new animal in {0}ms", localSP.ElapsedMilliseconds);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    curKey = Console.ReadKey().Key;
                    counter++;
                }
            }
        }
    }
}
