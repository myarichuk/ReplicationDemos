using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Client.Document;
using Raven.Client.Indexes;

namespace IndexReplication
{
    public class User
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }
    }

    public class UserIndex : AbstractIndexCreationTask<User>
    {
        public UserIndex()
        {
            Map = users => from user in users
                select new
                {
                    Name = user.FirstName + " " + user.LastName
                };
        }
    }

    class Program
    {
        //assuming existance of DatabaseA -> replication -> DatabaseB
        static void Main(string[] args)
        {
            using (var store = new DocumentStore
            {
                Url = "http://localhost:8080",
                DefaultDatabase = "DatabaseA"
            })
            {
                store.Initialize();      
          
                new UserIndex().Execute(store);
            }
        }
    }
}
