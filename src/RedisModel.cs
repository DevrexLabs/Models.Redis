using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OrigoDB.Core;
using OrigoDB.Core.Proxy;


namespace OrigoDB.Models.Redis
{
    /// <summary>
    /// Redis clone for OrigoDB
    /// </summary>
    public class RedisModel : Model
    {
        public enum KeyType
        {
            None,
            String,
            List,
            Hash,
            Set,
            SortedSet
        }

        public enum AggregateType
        {
            Sum,
            Min,
            Max
        }

        private readonly Random _random = new Random();

        /// <summary>
        /// This is where all the data goes
        /// </summary>
        private readonly Dictionary<string, object> _structures = new Dictionary<string, object>();

        /// <summary>
        /// Removes the specified keys. A key is ignored if it does not exist.
        /// Returns number of keys removed.
        /// </summary>
        [Command]
        public int Delete(params string[] keys)
        {
            return keys.Count(key => _structures.Remove(key));
        }

        /// <summary>
        /// Remove all the keys from the database, same as FLUSHALL or FLUSHDB
        /// </summary>
        public void Clear()
        {
            _structures.Clear();
        }

        /// <summary>
        /// Return the total number of keys, corresponds to DBSIZE
        /// </summary>
        /// <returns></returns>
        public int KeyCount()
        {
            return _structures.Count;
        }

        /// <summary>
        /// Returns true if key exists
        /// </summary>
        public bool Exists(string key)
        {
            return _structures.ContainsKey(key);
        }

        /// <summary>
        /// Returns the string representation of the type of the value stored at key. 
        /// The different types that can be returned are: string, list, set, zset and hash.
        /// </summary>
        /// <param name="key"></param>
        /// <returns> type of key, or KeyType.None when key does not exist</returns>
        public KeyType Type(string key)
        {
            object value;
            if (_structures.TryGetValue(key, out value))
            {
                if (value is StringBuilder) return KeyType.String;
                if (value is SortedSet<ZSetEntry>) return KeyType.SortedSet;
                if (value is Dictionary<string, string>) return KeyType.Hash;
                if (value is List<string>) return KeyType.List;
                if (value is HashSet<string>) return KeyType.Set;
            }
            return KeyType.None;
        }


        /// <summary>
        /// If key already exists and is a string, this command appends the value at the end of the string.
        /// If key does not exist it is created and set as an empty string,
        /// so APPEND will be similar to SET in this special case.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>length of the string after the append operation</returns>
        [Command]
        public int Append(string key, string value)
        {
            var sb = GetStringBuilder(key, create: true);
            return sb.Append(value).Length;
        }

        /// <summary>
        /// Set key to hold the string value. If key already holds a value, it is overwritten,
        /// regardless of its type. Any previous time to live associated 
        /// with the key is discarded on successful SET operation.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, string value)
        {
            _structures[key] = new StringBuilder(value);
        }

        /// <summary>
        /// Get the value of key. If the key does not exist the special value nil is returned. An error is returned
        /// if the value stored at key is not a string, because GET only handles string values.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get(string key)
        {
            var builder = GetStringBuilder(key);
            if (builder != null) return builder.ToString();
            else return null;
        }


        /// <summary>
        /// Count the number of set bits (population counting) in a string. By default all the bytes contained
        /// in the string are examined. It is possible to specify the counting operation only in an interval passing
        /// the additional arguments start and end. Like for the GETRANGE command start and end can contain negative
        /// values in order to index bytes starting from the end of the string, where -1 is the last byte, -2 is the
        /// penultimate, and so forth. Non-existent keys are treated as empty strings, so the command will return zero
        /// </summary>
        /// <param name="key"></param>
        /// <param name="startByte"></param>
        /// <param name="endByte"></param>
        /// <returns>the number of bits set to 1</returns>
        public int BitCount(string key, int startByte = 0, int endByte = Int32.MaxValue)
        {

            int bits = 0;
            var sb = GetStringBuilder(key);

            if (sb != null)
            {
                if (startByte < 0) startByte = sb.Length + startByte;
                if (endByte == int.MaxValue) endByte = sb.Length - 1;
                if (endByte < 0) endByte = sb.Length + endByte;
                for (int i = startByte; i <= endByte; i++)
                {
                    if (i >= sb.Length) break;
                    int n = sb[i];
                    while (n != 0)
                    {
                        bits++;
                        n &= n - 1;
                    }
                }
            }
            return bits;
        }

        /// <summary>
        /// Atomically sets key to value and returns the old value stored at key.
        /// Returns an error when key exists but does not hold a string value.
        /// </summary>
        [Command]
        public string GetSet(string key, string value)
        {
            var sb = GetStringBuilder(key, create: true);
            var oldValue = sb.ToString();
            sb.Clear().Append(value);
            return oldValue;
        }



        /// <summary>
        /// Decrements the number stored at key by decrement. If the key does not exist,
        /// it is set to 0 before performing the operation. An error is returned if the key
        /// contains a value of the wrong type or contains a string that can not be represented
        /// as integer. This operation is limited to 64 bit signed integers.
        /// </summary>
        [Command]
        public long DecrementBy(string key, long delta)
        {
            long decrementedValue = 0 - delta;
            var sb = GetStringBuilder(key);
            if (sb == null) _structures[key] = new StringBuilder().Append(decrementedValue);
            else
            {
                decrementedValue = Int64.Parse(sb.ToString()) - delta;
                sb.Clear();
                sb.Append(decrementedValue);
            }
            return decrementedValue;
        }


        /// <summary>
        /// Decrements the number stored at key by one. If the key does not exist, it is set to 0 before performing the operation.
        /// An error is returned if the key contains a value of the wrong type or contains a string that can not be represented as integer.
        /// This operation is limited to 64 bit signed integers.
        /// </summary>
        [Command]
        public long Decrement(string key)
        {
            return DecrementBy(key, 1);
        }

        /// <summary>
        /// Increments the number stored at key by one. If the key does not exist, it is set to 0 before performing the operation.
        /// An error is returned if the key contains a value of the wrong type or contains a string that can not be represented as integer.
        /// This operation is limited to 64 bit signed integers.
        /// </summary>
        [Command]
        public long Increment(string key)
        {
            return DecrementBy(key, -1);
        }

        /// <summary>
        /// Increments the number stored at key by a given value. If the key does not exist, it is set to 0 before performing the operation.
        /// An error is returned if the key contains a value of the wrong type or contains a string that can not be represented as integer.
        /// This operation is limited to 64 bit signed integers.
        /// </summary>
        [Command]
        public long IncrementBy(string key, long delta)
        {
            return DecrementBy(key, -delta);
        }

        /// <summary>
        /// Returns all keys matching pattern.
        /// While the time complexity for this operation is O(N), 
        /// the constant times are fairly low. For example, Redis running
        /// on an entry level laptop can scan a 1 million key database in 40 milliseconds.
        /// </summary>
        /// <param name="regex"></param>
        /// <returns></returns>
        public string[] Keys(string regex = ".*")
        {
            return _structures.Keys.Where(k => Regex.IsMatch(k, regex)).ToArray();
        }

        /// <summary>
        /// Returns the values of all specified keys. For every key that does not hold a string value or does not exist,
        /// the special value nil is returned. Because of this, the operation never fails.
        /// </summary>
        public string[] MGet(params string[] keys)
        {
            var result = new List<string>();
            foreach (var key in keys)
            {
                var sb = GetStringBuilder(key);
                if (sb != null) result.Add(sb.ToString());
                else result.Add(null);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Sets the given keys to their respective values. MSET replaces existing values with new values, just as regular SET. See MSETNX if you don't want to overwrite existing values.
        /// MSET is atomic, so all given keys are set at once. It is not possible for clients to see that some of the keys were updated while others are unchanged.
        /// </summary>
        /// <param name="interlacedKeysAndValues"></param>
        [Command]
        public void MSet(params string[] interlacedKeysAndValues)
        {
            foreach (var pair in ToPairs(interlacedKeysAndValues))
            {
                Set(pair.Item1, pair.Item2);
            }
        }

        /// <summary>
        /// Sets the specified fields to their respective values in the hash stored at key.
        /// This command overwrites any existing fields in the hash.
        /// If key does not exist, a new key holding a hash is created.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="interlacedKeysAndValues"></param>
        [Command]
        public void HMSet(string key, params string[] interlacedKeysAndValues)
        {
            foreach (var pair in ToPairs(interlacedKeysAndValues))
            {
                HSet(key, pair.Item1, pair.Item2);
            }
        }

        /// <summary>
        /// Returns the values associated with the specified fields in the hash stored at key.
        /// For every field that does not exist in the hash, a nil value is returned.
        /// Because a non-existing keys are treated as empty hashes, running HMGET
        /// against a non-existing key will return a list of nil values.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        public string[] HMGet(string key, params string[] fields)
        {
            var hash = GetHash(key);
            if (hash == null) return new string[fields.Length];
            var result = new List<string>();
            foreach (var field in fields)
            {
                string val;
                hash.TryGetValue(field, out val);
                result.Add(val);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Returns the length of the string value stored at key. An error is returned when key holds a non-string value
        /// </summary>
        /// <param name="key"></param>
        /// <returns>the length of the string at key, or 0 when key does not exist.</returns>
        public int StrLength(string key)
        {
            var sb = GetStringBuilder(key);
            if (sb == null) return 0;
            return sb.Length;
        }

        /// <summary>
        /// Return a random key from the key space or null if there are no keys.
        /// </summary>
        /// <returns></returns>
        public string RandomKey()
        {
            if (_structures.Count == 0) return null;
            //todo: optimize, should be O(1) not O(N)
            int randomIndex = _random.Next(_structures.Count);
            return _structures.Skip(randomIndex).Select(kvp => kvp.Key).First();
        }


        /// <summary>
        /// Renames key to newkey. It returns an error when the source and destination names are the same,
        /// or when key does not exist. If newkey already exists it is overwritten, when this happens RENAME executes an
        /// implicit DEL operation, so if the deleted key contains a very big value it may cause high latency
        /// even if RENAME itself is usually a constant-time operation.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="newkey"></param>
        public void Rename(string key, string newkey)
        {
            if (key == newkey) throw new CommandAbortedException("newkey cannot be same as key");
            if (!_structures.ContainsKey(key)) throw new CommandAbortedException("no such key");
            _structures[newkey] = _structures[key];
            _structures.Remove(key);
        }

        /// <summary>
        /// Sets field in the hash stored at key to value. If key does not exist, a new key holding a hash is created. If field already exists in the hash, it is overwritten
        /// </summary>
        /// <param name="key"></param>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns>true if field is a new field in the hash, 0 if field existed</returns>
        [Command]
        public bool HSet(string key, string field, string value)
        {
            var hash = GetHash(key, create: true);
            bool existing = hash.ContainsKey(field);
            hash[field] = value;
            return existing;
        }

        /// <summary>
        /// Removes the specified fields from the hash stored at key. 
        /// Specified fields that do not exist within this hash are ignored. 
        /// If key does not exist, it is treated as an empty hash and
        /// this command returns 0.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="fields"></param>
        /// <returns> the number of fields that were removed from the hash,
        /// not including specified but non existing fields.</returns>
        [Command]
        public int HDelete(string key, params string[] fields)
        {
            var hash = GetHash(key);
            if (hash == null) return 0;
            return fields.Count(hash.Remove);
        }

        /// <summary>
        /// Returns if field is an existing field in the hash stored at key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="field"></param>
        /// <returns>true if hash contains field, otherwise false</returns>
        public bool HExists(string key, string field)
        {
            var hash = GetHash(key);
            if (hash == null) return false;
            return hash.ContainsKey(field);
        }

        /// <summary>
        /// Returns the value associated with field in the hash stored at key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="field"></param>
        /// <returns></returns>
        public string HGet(string key, string field)
        {
            string result = null;
            var hash = GetHash(key);
            if (hash != null) hash.TryGetValue(field, out result);
            return result;
        }

        /// <summary>
        /// Returns all fields and values of the hash stored at key.
        /// In the returned value, every field name is followed by its
        /// value, so the length of the reply is twice the size of the hash
        /// </summary>
        /// <param name="key"></param>
        /// <returns> list of fields and their values stored in the hash, or an empty list when key does not exist.</returns>
        public string[] HGetAll(string key)
        {
            var hash = GetHash(key);
            if (hash == null) return new string[0];

            var result = new string[hash.Count * 2];
            int i = 0;
            foreach (KeyValuePair<string, string> kvp in hash)
            {
                result[i] = kvp.Key;
                result[i + 1] = kvp.Value;
                i += 2;
            }
            return result;
        }

        /// <summary>
        /// Removes the specified fields from the hash stored at key.
        /// Specified fields that do not exist within this hash are ignored.
        /// If key does not exist, it is treated as an empty hash and this command
        /// returns 0.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="field"></param>
        /// <param name="delta"></param>
        /// <returns></returns>
        [Command]
        public long HIncrementBy(string key, string field, long delta)
        {
            long newVal = 0 + delta;
            var hash = GetHash(key, create: true);
            string val;
            if (hash.TryGetValue(field, out val))
            {
                if (!Int64.TryParse(val, out newVal))
                {
                    if (hash.Count == 0) hash.Remove(key);
                    throw new CommandAbortedException("Not a number");
                }
                newVal += delta;
            }
            hash[field] = newVal.ToString();
            return newVal;
        }

        /// <summary>
        /// Get the number of fields in a hash or zero if key is missing
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int HLen(string key)
        {
            var hash = GetHash(key);
            if (hash == null) return 0;
            else return hash.Count;
        }

        /// <summary>
        /// Returns all field names in the hash stored at key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns> list of fields in the hash, or an empty list when key does not exist.</returns>
        public string[] HKeys(string key)
        {
            var hash = GetHash(key);
            if (hash == null) return new string[0];
            return hash.Keys.ToArray();
        }


        /// <summary>
        /// Returns all values in the hash stored at key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns> list of values in the hash, or an empty list when key does not exist.</returns>
        public string[] HValues(string key)
        {
            var hash = GetHash(key);
            if (hash == null) return new string[0];
            return hash.Values.ToArray();
        }

        /// <summary>
        /// Get an element from a list by its index
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        public string LIndex(string key, int index)
        {

            var list = GetList(key);
            if (index <= 0) index += list.Count;
            if (index >= 0 && index < list.Count)
            {
                return list[index];
            }
            return null;
        }

        /// <summary>
        /// Insert all the specified values at the head of the list stored at key.
        /// If key does not exist, it is created as empty list before
        /// performing the push operations. When key holds a value that is not a list, an error is returned.
        /// </summary>
        /// <returns>length of the list after the push operations</returns>
        [Command]
        public int LPush(string key, params string[] values)
        {
            return NPush(key, head: true, values: values);
        }


        /// <summary>
        /// Insert all the specified values at the tail of the list stored at key. If key does not exist, it is created as empty list before performing the push operation. When key holds a value that is not a list, an error is returned.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        [Command]
        public int RPush(string key, params string[] values)
        {
            return NPush(key, head: false, values: values);
        }

        /// <summary>
        /// Helper shared by RPush and LPush
        /// </summary>
        /// <param name="key"></param>
        /// <param name="head"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        private int NPush(string key, bool head, params string[] values)
        {
            var list = GetList(key, create: true);
            int idx = head ? 0 : list.Count;
            list.InsertRange(idx, values);
            int result = list.Count;
            if (list.Count == 0) _structures.Remove(key);
            return result;
        }

        /// <summary>
        /// Inserts value in the list stored at key either before or after the reference
        /// value pivot. When key does not exist, it is considered an empty list and no
        /// operation is performed. An error is returned when key exists but does not
        /// hold a list value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="pivot"></param>
        /// <param name="value"></param>
        /// <param name="before"></param>
        /// <returns> the length of the list after the insert operation, or -1 when the value pivot was not found.</returns>
        [Command]
        public int LInsert(string key, string pivot, string value, bool before = true)
        {
            var list = GetList(key);
            if (list == null) return 0;

            int idx = list.IndexOf(pivot);
            if (idx == -1) return -1;

            if (!before) idx++;
            list.Insert(idx, value);
            return list.Count;
        }

        /// <summary>
        /// Returns the length of the list stored at key.
        /// If key does not exist, it is interpreted as an empty list and 0 is returned.
        /// An error is returned when the value stored at key is not a list
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int LLength(string key)
        {
            var list = GetList(key);
            if (list == null) return 0;
            return list.Count;
        }

        /// <summary>
        /// Removes and returns the first element of the list stored at key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns> the value of the first element, or nil when key does not exist.</returns>
        [Command]
        public string LPop(string key)
        {
            var list = GetList(key);
            if (list == null || list.Count == 0) return null;
            var result = list[0];
            list.RemoveAt(0);
            return result;
        }

        /// <summary>
        /// Removes and returns the last element of the list stored at key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>the value of the last element, or nil when key does not exist.</returns>
        [Command]
        public string RPop(string key)
        {
            var list = GetList(key);
            if (list == null || list.Count == 0) return null;
            var result = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return result;
        }

        /// <summary>
        /// Sets the list element at index to value. For more information on the index argument, see LINDEX.
        /// An error is returned for out of range indexes
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void LSet(string key, int index, string value)
        {
            var list = GetList(key);
            if (list == null) throw new CommandAbortedException("No such key");
            if (index < 0) index += list.Count;
            if (index < 0 || index >= list.Count) throw new CommandAbortedException("Index out of range");
            list[index] = value;
        }

        /// <summary>
        /// Add the specified members to the set stored at key.
        /// Specified members that are already a member of this set
        /// are ignored. If key does not exist, a new set is created before
        /// adding the specified members
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns>the number of elements that were added to the set, not including all the elements already present into the set.</returns>
        [Command]
        public int SAdd(string key, params string[] values)
        {
            var set = GetSet(key, create: true);
            return values.Count(set.Add);
        }

        /// <summary>
        /// Returns the set cardinality (number of elements) of the set stored at key
        /// </summary>
        /// <param name="key"></param>
        /// <returns> the cardinality (number of elements) of the set, or 0 if key does not exist.</returns>
        public int SCard(string key)
        {
            var set = GetSet(key);
            if (set == null) return 0;
            return set.Count;
        }

        /// <summary>
        /// Returns the members of the set resulting from the
        /// difference between the first set and all the successive sets.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="setsToSubstract"></param>
        /// <returns></returns>
        public string[] SDiff(string key, params string[] setsToSubstract)
        {
            IEnumerable<string> set = GetSet(key);
            if (set == null) return new string[0];

            var empty = new HashSet<string>();
            return setsToSubstract
                .Aggregate(set, (current, s) => current.Except(GetSet(s) ?? empty))
                .ToArray();
        }

        /// <summary>
        /// This command is equal to SDIFF, but instead of returning the resulting set, it is stored in destination. If destination already exists, it is overwritten.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="key"></param>
        /// <param name="setsToSubstract"></param>
        /// <returns>the number of elements in the resulting set.</returns>
        [Command]
        public int SDiffStore(string destination, string key, params string[] setsToSubstract)
        {
            var members = SDiff(key, setsToSubstract);
            if (members.Length > 0)
            {
                _structures[destination] = new HashSet<string>(members);
            }
            return members.Length;
        }

        /// <summary>
        /// Returns the members of the set resulting from the intersection
        /// of all the given sets. Keys that do not exist are considered to be
        /// empty sets. With one of the keys being an empty set, the resulting
        /// set is also empty (since set intersection with an empty set always results in an empty set).
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keys"></param>
        /// <returns> list with members of the resulting set</returns>
        public string[] SInter(string key, params string[] keys)
        {
            IEnumerable<string> set = GetSet(key);
            if (set == null) return new string[0];

            var empty = new HashSet<string>();
            return keys
                .Aggregate(set, (current, s) => current.Intersect(GetSet(s) ?? empty))
                .ToArray();
        }

        /// <summary>
        /// This command is equal to SINTER, but instead of returning the resulting set, it is stored in destination.
        /// If destination already exists, it is overwritten.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="key"></param>
        /// <param name="keys"></param>
        /// <returns>the number of elements in the resulting set.</returns>
        [Command]
        public int SInterStore(string destination, string key, params string[] keys)
        {
            var members = SInter(key, keys);
            if (members.Length == 0) return 0;
            _structures[destination] = new HashSet<string>(members);
            return members.Length;
        }

        /// <summary>
        /// Returns if member is a member of the set stored at key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>true if the set exists and value is a member, otherwise false</returns>
        public bool SIsMember(string key, string value)
        {
            var set = GetSet(key);
            return set != null && set.Contains(value);
        }

        /// <summary>
        /// Returns all the members of the set value stored at key.
        /// This has the same effect as running SINTER with one argument key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>all elements of the set</returns>
        public string[] SMembers(string key)
        {
            return SInter(key);
        }

        /// <summary>
        /// Move member from the set at source to the set at destination. This operation is atomic. In every given moment the element will appear to be a member of source or destination for other clients.
        /// If the source set does not exist or does not contain the specified element, no operation is performed and 0 is returned. Otherwise, the element is removed from the source set and added to the destination set. When the specified element already exists in the destination set, it is only removed from the source set.
        /// An error is returned if source or destination does not hold a set value.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="value"></param>
        /// <returns>true if the element was moved</returns>
        [Command]
        public bool SMove(string source, string destination, string value)
        {
            bool removed = false;
            var sourceSet = GetSet(source);
            if (sourceSet == null) return false;
            var destinationSet = GetSet(destination, create: true);
            if (sourceSet.Remove(value)) removed = true;
            if (removed) destinationSet.Add(value);
            if (destinationSet.Count == 0) _structures.Remove(destination);
            return removed;
        }

        /// <summary>
        /// Removes and returns a random element from the set value stored at key.
        /// This operation is similar to SRANDMEMBER, that returns a random element from a set but does not remove it.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>the removed element, or nil when key does not exist.</returns>
        [Command]
        public string SPop(string key)
        {
            var result = SRandMember(key);
            if (result.Length == 0) return null;
            GetSet(key).Remove(result[0]);
            return result[0];
        }

        /// <summary>
        /// When called with just the key argument, return a random element from the set value stored at key.
        /// Starting from Redis version 2.6, when called with the additional count argument, return an array of
        /// count distinct elements if count is positive. If called with a negative count the behavior changes
        /// and the command is allowed to return the same element multiple times. In this case the numer of returned
        /// elements is the absolute value of the specified count. When called with just the key argument, the operation
        /// is similar to SPOP, however while SPOP also removes the randomly selected element from the set, SRANDMEMBER will
        /// just return a random element without altering the original set in any way.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public string[] SRandMember(string key, int count = 1)
        {
            var set = GetSet(key);
            if (set == null) return null;
            if (set.Count == 0 || count == 0) return new string[0];
            bool allowDuplicates = count < 0;
            if (allowDuplicates) count = -count;

            if (!allowDuplicates && count >= set.Count) return set.ToArray();

            var result = new Dictionary<int, string>();
            var randomIndicies = allowDuplicates ? (ICollection<int>)new List<int>(count) : new HashSet<int>();
            while (randomIndicies.Count < count) randomIndicies.Add(_random.Next(count));

            int i = 0;
            foreach (var member in set)
            {
                if (randomIndicies.Contains(i))
                {
                    result[i++] = member;
                }
            }

            return randomIndicies.Select(idx => result[idx]).ToArray();
        }

        /// <summary>
        /// Remove the specified members from the set stored at key. Specified members that are not a member of this set are
        /// ignored. If key does not exist, it is treated as an empty set and this command returns 0.
        /// An error is returned when the value stored at key is not a set.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="members"></param>
        /// <returns> the number of members that were removed from the set, not including non existing members.</returns>
        [Command]
        public int SRemove(string key, params string[] members)
        {
            var set = GetSet(key);
            if (set == null || set.Count == 0) return 0;
            return members.Count(set.Remove);
        }

        /// <summary>
        /// Returns the members of the set resulting from the union
        /// of all the given sets.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public string[] SUnion(string key, params string[] keys)
        {
            IEnumerable<string> set = GetSet(key) ?? new HashSet<string>();
            set = keys.Select(k => GetSet(k) ?? new HashSet<string>())
                .Aggregate(set, (current, s) => current.Union(s));
            return set.ToArray();
        }

        /// <summary>
        /// This command is equal to SUNION, but instead of returning the resulting set, it is stored in destination.
        /// If destination already exists, it is overwritten.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="key"></param>
        /// <param name="keys"></param>
        /// <returns>the number of elements in the resulting set</returns>
        [Command]
        public int SUnionStore(string destination, string key, params string[] keys)
        {
            string[] items = SUnion(key, keys);
            if (items.Length > 0) _structures[destination] = new HashSet<string>(items);
            return items.Length;
        }

        [Command]
        public bool ZAdd(string key, string member, double score)
        {
            return ZAdd(key, member, score.ToString()) == 1;
        }

        [Command]
        public int ZAdd(string key, KeyValuePair<string, double>[] items)
        {
            var d = items.ToDictionary(item => item.Key, item => item.Value);
            return ZAdd(key, d);
        }

        [Command]
        public int ZAdd(string key, IDictionary<string, double> membersAndScores)
        {
            var sortedSet = GetSortedSet(key, create: true);

            int elementsAdded = membersAndScores.Count;
            foreach (var entry in membersAndScores.Select(ms => new ZSetEntry(ms.Value, ms.Key)))
            {
                if (sortedSet.Remove(entry)) elementsAdded--;
                sortedSet.Add(entry);
            }
            if (sortedSet.Count == 0) _structures.Remove(key);
            return elementsAdded;
            
        }

        /// <summary>
        /// Adds all the specified members with the specified scores to the sorted set stored at key. It is possible to specify
        /// multiple score/member pairs. If a specified member is already a member of the sorted set, the score is updated and the element reinserted at the right position to ensure the correct ordering. If key does not exist, a new sorted set with the specified members as sole members is created, like if the sorted set was empty. If the key exists but does not hold a sorted set, an error is returned.
        /// The score values should be the string representation of a numeric value, and accepts double precision floating point numbers.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="scoreAndMembersInterlaced"></param>
        /// <returns>The number of elements added to the sorted sets, not including elements already existing for which the score was updated.</returns>
        [Command]
        public int ZAdd(string key, params string[] scoreAndMembersInterlaced)
        {
            try
            {
                var d = ToPairs(scoreAndMembersInterlaced).ToDictionary(p => p.Item2, p => double.Parse(p.Item1));
                return ZAdd(key, d);
            }
            catch (FormatException)
            {
                throw new CommandAbortedException("value is not a valid float");
            }
        }

        /// <summary>
        /// Get the number of members in a sorted set
        /// </summary>
        /// <param name="key"></param>
        /// <returns>the cardinality (number of elements) of the sorted set, or 0 if key does not exist</returns>
        public int ZCard(string key)
        {
            var sortedSet = GetSortedSet(key);
            return sortedSet == null ? 0 : sortedSet.Count;
        }

        /// <summary>
        /// Count the members in a sorted set with scores within a given range
        /// </summary>
        public int ZCount(string key, double min, double max)
        {
            var sortedSet = GetSortedSet(key);
            if (sortedSet == null) return 0;
            return sortedSet
                .SkipWhile(entry => entry.Score < min)
                .TakeWhile(entry => entry.Score <= max).Count();
        }

        /// <summary>
        /// Increments the score of member in the sorted set stored at key by increment.
        /// </summary>
        [Command]
        public double ZIncrementBy(string key, double increment, string member)
        {
            var sortedSet = GetSortedSet(key);
            var entry = sortedSet.SingleOrDefault(e => e.Value == member);
            if (entry != null) sortedSet.Remove(entry);
            else entry = new ZSetEntry(0, member);
            entry = entry.Increment(increment);
            sortedSet.Add(entry);
            return entry.Score;
        }

        /// <summary>
        /// Computes the intersection of the sorted sets identified by keys, and stores the result in destination.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        /// <param name="weights"></param>
        /// <param name="aggregateType"></param>
        /// <returns></returns>
        [Command]
        public int ZInterStore(string destination, string[] keys, double[] weights = null, AggregateType aggregateType = AggregateType.Sum)
        {
            var sets = keys.Select(k => GetSortedSet(k) ?? new SortedSet<ZSetEntry>()).ToArray();
            if (sets.Any(s => s.Count == 0)) return 0;
            return ZSetOperationAndStoreImpl(destination, sets, weights, aggregateType, false);
        }

        private int ZSetOperationAndStoreImpl(string destination, SortedSet<ZSetEntry>[] sets, double[] weights,
            AggregateType aggregateType, bool isUnionOperation)
        {
            if (weights != null && weights.Length != sets.Length)
            {
                throw new CommandAbortedException("number of weights must correspond to number of keys");
            }

            Func<int, double> weightOf = idx => weights == null ? 1.0 : weights[idx];
            Func<double, double, double> aggregator = (a, b) =>
                aggregateType == AggregateType.Sum
                    ? a + b
                    : aggregateType == AggregateType.Max ? Math.Max(a, b) : Math.Min(a, b);

            var newSet = new SortedSet<ZSetEntry>(
                sets
                    .SelectMany((set, idx) => set.Select(e => new ZSetEntry(e.Score * weightOf(idx), e.Value)))
                    .GroupBy(e => e.Value)
                    .Where(g =>  isUnionOperation || g.Count() == sets.Length)
                    .Select(g => new ZSetEntry(g.Select(e => e.Score).Aggregate(aggregator), g.Key))
                );

            if (newSet.Count > 0) _structures[destination] = newSet;
            return newSet.Count;
        }


        /// <summary>
        /// Returns the specified range of elements in the sorted set stored at key. 
        /// The elements are considered to be ordered from the lowest to the highest score.
        /// Lexicographical order is used for elements with equal score.
        /// </summary>
        /// <returns>the members of the set</returns>
        public string[] ZRange(string key, int start  = 0, int stop = -1)
        {
            return ZRangeImpl(key, start, stop).Select(pair => pair.Key).ToArray();
        }

        /// <summary>
        /// Returns the specified range of elements in the sorted set stored at key.
        /// The elements are considered to be ordered from the lowest to the highest score.
        /// Lexicographical order is used for elements with equal score.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public KeyValuePair<string, double>[] ZRangeWithScores(string key, int start = 0, int stop = -1)
        {
            return ZRangeImpl(key, start, stop).ToArray();

        }

        /// <summary>
        /// Returns all the elements in the sorted set at key with a score between min
        /// and max (including elements with score equal to min or max).
        /// The elements are considered to be ordered from low to high scores.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        public string[] ZRangeByScore(string key, double min, double max, int skip = 0, int take = Int32.MaxValue)
        {
            return ZRangeByScoreImpl(key, min, max, skip, take).Select(e => e.Value).ToArray();
        }

        /// <summary>
        /// Returns all the elements and scores in the sorted set at key with a score between min
        /// and max (including elements with score equal to min or max).
        /// The elements are considered to be ordered from low to high scores.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        public KeyValuePair<string,double>[] ZRangeByScoreWithScores(string key, double min, double max, int skip = 0, int take = Int32.MaxValue)
        {
            return ZRangeByScoreImpl(key, min, max, skip, take)
                .Select(e => new KeyValuePair<string, double>(e.Value, e.Score))
                .ToArray();
        }

        /// <summary>
        /// Returns the rank of member in the sorted set stored at key, with the scores ordered from low to high.
        /// The rank (or index) is 0-based, which means that the member with the lowest score has rank 0.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member"></param>
        /// <returns>the rank of member or null if the set or member does not exist</returns>
        public int? ZRank(string key, string member)
        {
            SortedSet<ZSetEntry> set = GetSortedSet(key);
            if (set == null) return null;
            int idx = -1;
            
            foreach (var entry in set)
            {
                idx++;
                if (entry.Value == member) return idx;
            }
            return null;
        }

        /// <summary>
        /// Removes the specified members from the sorted set stored at key. Non existing members are ignored.
        /// An error is returned when key exists and does not hold a sorted set.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="members"></param>
        /// <returns>The number of members removed from the sorted set, not including non existing members</returns>
        [Command]
        public int ZRemove(string key, params string[] members)
        {
            var set = GetSortedSet(key);
            if (set == null) return 0;
            return set.RemoveWhere(e => members.Contains(e.Value));
        }

        /// <summary>
        /// Removes all elements in the sorted set stored at key within rank between start and stop.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <returns>Number of elements removed</returns>
        [Command]
        public int ZRemoveRangeByRank(string key, int first, int last)
        {
            var set = GetSortedSet(key);
            if (set == null) return 0;
            return ZRange(key, first, last).Count(member => set.Remove(new ZSetEntry(0, member)));
        }

        /// <summary>
        /// Removes all elements in the sorted set stored at key with a score between min and max (inclusive).
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns>the number of elements removed</returns>
        [Command]
        public int ZRemoveRangeByScore(string key, double min, double max)
        {
            var set = GetSortedSet(key);
            if (set == null) return 0;
            return ZRangeByScoreImpl(key, min, max, 0, int.MaxValue).Count(set.Remove);
        }

        /// <summary>
        /// Returns the score of member in the sorted set at key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member"></param>
        /// <returns>the score or null when the set or member doesn't exist</returns>
        public double? ZScore(string key, string member)
        {
            var set = GetSortedSet(key);
            if (set == null) return null;
            return set
                .Where(e => e.Value == member)
                .Select(e => (double?) e.Score)
                .SingleOrDefault();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public string[] ZReverseRange(string key, int start, int stop)
        {
            var set = GetSortedSet(key);
            if (set == null) return new string[0];
            var range = new Range(start, stop).Flip(set.Count);
            return ZRange(key, range.FirstIdx, range.LastIdx).Reverse().ToArray();
        }

        public KeyValuePair<string,double>[] ZReverseRangeWithScores(string key, int start = 0, int stop = 0)
        {
            throw new NotImplementedException();
        }

        public string[] ZReverseRangeByScoreWithScores(string key, double min = double.MinValue,
            double max = double.MaxValue, int skip = 0, int take = Int32.MaxValue)
        {
            throw new NotImplementedException();
        }

        public int? ZReverseRank(string key, string member)
        {
            var rank = ZRank(key, member);
            if (rank.HasValue) return GetSortedSet(key).Count - rank.Value;
            return null;
        }

        /// <summary>
        /// Computes the union of numkeys sorted sets given by the specified keys,
        /// and stores the result in destination. 
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        /// <param name="weights"></param>
        /// <param name="aggregateType"></param>
        /// <returns></returns>
        [Command]
        public int ZUnionStore(string destination, string[] keys, double[] weights = null,
            AggregateType aggregateType = AggregateType.Sum)
        {
            var sets = keys.Select(k => GetSortedSet(k) ?? new SortedSet<ZSetEntry>()).ToArray();
            return ZSetOperationAndStoreImpl(destination, sets, weights, aggregateType, isUnionOperation: true);
        }


        private IEnumerable<ZSetEntry> ZRangeByScoreImpl(string key, double min, double max, int skip, int take)
        {
            var set = GetSortedSet(key);
            if (set == null) return new ZSetEntry[0];
            return
                set.SkipWhile(e => e.Score < min)
                    .TakeWhile(e => e.Score <= max)
                    .Skip(skip)
                    .Take(take);
        }

        private IEnumerable<KeyValuePair<string, double>> ZRangeImpl(string key, int start, int stop)
        {
            var sortedSet = GetSortedSet(key);
            if (sortedSet == null) return new KeyValuePair<string, double>[0];

            var range = new Range(start, stop, sortedSet.Count);
            return
                sortedSet.Skip(range.FirstIdx)
                    .Take(range.Length)
                    .Select(e => new KeyValuePair<string, double>(e.Value, e.Score));
        }

        private SortedSet<ZSetEntry> GetSortedSet(string key, bool create = false)
        {
            return As<SortedSet<ZSetEntry>>(key, create, @throw: false);
        }

        private HashSet<string> GetSet(string key, bool create = false, bool @throw = false)
        {
            return As<HashSet<string>>(key, create, @throw);
        }

        private List<String> GetList(string key, bool create = false, bool @throw = false)
        {
            return As<List<String>>(key, create, @throw);
        }

        private Dictionary<string, string> GetHash(string key, bool create = false, bool @throw = false)
        {
            return As<Dictionary<string, string>>(key, create, @throw);
        }

        private StringBuilder GetStringBuilder(string key, bool create = false, bool @throw = false)
        {
            return As<StringBuilder>(key, create, @throw);
        }

        private T As<T>(string key, bool create, bool @throw = false) where T : class, new()
        {
            var result = GetStructure<T>(key);
            if (result == null)
            {
                if (create) _structures[key] = result = new T();
                else if (@throw) throw new CommandAbortedException("Key missing");
            }
            return result;
        }

        private T GetStructure<T>(string key) where T : class
        {
            object val;
            if (_structures.TryGetValue(key, out val))
            {
                if (val is T) return (T)val;
                throw new CommandAbortedException("WRONGTYPE Operation against a key holding the wrong kind of value");
            }
            return null;
        }

        private IEnumerable<Tuple<string, string>> ToPairs(string[] interlaced)
        {
            if (interlaced.Length % 2 != 0)
            {
                throw new CommandAbortedException("Odd number of arguments to MSet/HMSet");
            }

            for (int i = 0; i < interlaced.Length; i += 2)
            {
                yield return Tuple.Create(interlaced[i], interlaced[i + 1]);
            }
        }

        private class ZSetEntry : IComparable<ZSetEntry>
        {
            public readonly double Score;
            public readonly string Value;

            public ZSetEntry(double score, string value)
            {
                Score = score;
                Value = value;
            }

            public int CompareTo(ZSetEntry other)
            {
                int result = Math.Sign(Score - other.Score);
                if (result == 0) result = String.Compare(Value, other.Value, StringComparison.InvariantCulture);
                return result;
            }

            public override bool Equals(object obj)
            {
                var other = obj as ZSetEntry;
                if (ReferenceEquals(other, null)) return false;
                return Value == other.Value;
            }

            public override int GetHashCode()
            {
                return Value.GetHashCode();
            }

            internal ZSetEntry Increment(double increment)
            {
                return new ZSetEntry(Score + increment, Value);
            }
        }



    }
}