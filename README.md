Models.Redis
============

A redis clone for OrigoDB implemented in C#. Uses OrigoDB for persistence and transaction
gaurantees. The basic commands for keys, strings, lists, sets, hashes and sorted sets are implemented.

The implementation is a single class, RedisModel. Redis commands correspond to methods with
roughly the same name, signatures and behavior.  See the docs http://redis.io/commands

Commands for cursors, transactions, scripting, server/connection commands
### Unimplemented commands
* pub/sub
* expiration
* add more tests
* cursors - n/a unless running remote
* transactions
* scripting
* server/connection - n/a, uses the OrigoDB infrastructure


## Example

```csharp
   //create transparent persistence proxy using command logging (AOF)
   var db = Db.For<RedisModel>();
   
   //call methods on the proxy
   db.SAdd("fruit", "banana");
   db.SAdd("fruit", "apple");
   int actual = db.SCard("fruit");
   
   //Set cardinality should be 2
   Assert.AreEqual(2,actual);
```




