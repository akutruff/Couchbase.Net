//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//namespace FastCouch
//{
//    //Treat as immutable!
//    public class DBCluster
//    {
//        //This is something that we need to persist and make decisions upon as the cluster changes. Only as an example.
//        public int Version { get; set; }
//        public Dictionary<string, DBServer> Servers { get; private set; }

//        public DBCluster(Dictionary<string, DBServer> servers)
//        {
//            Servers = servers;
//        }
//    }

//    //Immutable as well
//    public class DBServer
//    {
//        public bool TryGet(string key, out string result)
//        {
//            result = "Foo";
//            //May fail.
//            return true;
//        }
//    }

//    public class DBClient
//    {
//        private volatile DBCluster _currentCluster;
//        private object _gate = new object();

//        public DBClient()
//        {
//        }

//        public string GetByKey(string key)
//        {            
//            bool wasSuccesful;
//            string result;
//            do
//            {
//                //This is key: copy the reference to the current shared state from the volatile member on to the stack.  
//                //  Again, this is a reference copy, not a deep copy.  The .NET memory model will make sure that your
//                //  CPU cache is current and updated.

//                DBCluster cluster = _currentCluster;

//                //Optimistically do something here like try to get from the server you expect to have the key.  
//                //  However, this operation must be failable/retryable because the cluster could have changed.  
//                //  Also, this whole pattern will only be worthwhile if the cluster is updated infrequently.

//                var serverForKey = cluster.Servers[key];
//                wasSuccesful = serverForKey.TryGet(key, out result);

//            } while (!wasSuccesful);
            

//            return result;
//        }

//        //assume a bunch of threads may be trying to update the cluster simultaneously.
//        public void UpdateCluster(Dictionary<string, DBServer> newSetOfServers)
//        {
//            //Must create an entirely new state object, and cannot simply update the members of the object pointed to by _currentCluster, even if you do it in the lock.  
//            //  It is okay to read the data from the object in _currentCluster as long as it is in the lock.
//            var newCluster = new DBCluster(newSetOfServers);

//            lock (_gate)
//            {
//                //Technically do not need to do a snap here, but it prevents multiple volatile reads (cache flushes) rather than accessing the member many times in this lock;
//                //  also, the compiler will be allowed to do instruction reordering after this line.
//                var currentCluster = _currentCluster;
                
//                //Since we're reading the state from the current cluster, this is why we are in the lock, because many threads could be trying to update the cluster simultatenously
//                //  and we could be doing a bunch of work here that cannot be executed in parallel, like establishing Http connections to servers that do not already exist in the cluster.
//                newCluster.Version = _currentCluster.Version + 1;

//                // Also, we may not even want to update the cluster... Hence, another example of when a lock is likely needed.
//                if (currentCluster.Servers.Count == newSetOfServers.Count)
//                {
//                    return;
//                }

//                //This is key...  Only update the member variable in the very last line of the lock block.  
//                //  Otherwise, the functions like Get, that do not use locks would get an incomplete cluster configuration.
//                _currentCluster = newCluster;
//            }
//        }
//    }
//}
