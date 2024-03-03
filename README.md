# MemKV

_An in-memory key-value store, following the Redis protocol, built on F#._

Inspired by [Build Your Own Redis Server](https://codingchallenges.fyi/challenges/challenge-redis) by [Coding Challenges](https://codingchallenges.fyi/).

## Release Mode Benchmark

MemKV can run about 100.000+ operations per second when running locally.

```shell
redis-benchmark -t SET,GET -n 100000 -c 10
```

Output:

```
====== SET ======
  100000 requests completed in 0.59 seconds
  10 parallel clients
  3 bytes payload
  keep alive: 1
  multi-thread: no

99.57% <= 0.1 milliseconds
99.68% <= 0.2 milliseconds
99.69% <= 0.3 milliseconds
99.83% <= 0.4 milliseconds
99.89% <= 0.5 milliseconds
99.93% <= 0.6 milliseconds
99.95% <= 0.7 milliseconds
99.95% <= 0.8 milliseconds
99.95% <= 1.0 milliseconds
99.96% <= 1.4 milliseconds
99.97% <= 1.8 milliseconds
99.98% <= 2 milliseconds
99.99% <= 4 milliseconds
99.99% <= 5 milliseconds
100.00% <= 6 milliseconds
100.00% <= 7 milliseconds
100.00% <= 7 milliseconds
170357.75 requests per second

====== GET ======
  100000 requests completed in 0.57 seconds
  10 parallel clients
  3 bytes payload
  keep alive: 1
  multi-thread: no

100.00% <= 1 milliseconds
100.00% <= 1 milliseconds
174216.03 requests per second
```

