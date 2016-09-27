# Deprecated!

The RedisModel is now included in OrigoDb.Core
https://github.com/devrexlabs/origodb

Models.Redis
============
A redis clone for OrigoDB implemented in C#. Uses OrigoDB for persistence and transaction
gaurantees. The basic commands for keys, strings, lists, sets, hashes and sorted sets are implemented.

The implementation is a single class, RedisModel. Redis commands correspond to methods with
roughly the same name, signatures and behavior.  See the docs http://redis.io/commands


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




