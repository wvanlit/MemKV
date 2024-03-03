module MemKV.Protocol.Tests.MessageTests

open Expecto
open MemKV.Protocol

let tests =
    testList
        "Message"
        [ test "Parses Simple String" {
              let input = "+OK\r\n"
              let result = Message.parseMessage input
              let expected = SimpleString "OK"
              Expect.equal result expected "OK"
          }
          test "Parses Error" {
              let input = "-ERR\r\n"
              let result = Message.parseMessage input
              let expected = Error "ERR"
              Expect.equal result expected "ERR"
          }
          test "Parses Integer" {
              let input = ":1000\r\n"
              let result = Message.parseMessage input
              let expected = Integer 1000
              Expect.equal result expected "1000"
          }
          test "Parses NULL Bulk String" {
              let input = "$-1\r\n"
              let result = Message.parseMessage input
              let expected = BulkString None
              Expect.equal result expected "NULL Bulk String"
          }
          test "Parses Empty Bulk String" {
              let input = "$0\r\n\r\n"
              let result = Message.parseMessage input
              let expected = BulkString(Some "")
              Expect.equal result expected "Empty Bulk String"
          }
          test "Parses Bulk String" {
              let input = "$5\r\nHello\r\n"
              let result = Message.parseMessage input
              let expected = BulkString(Some "Hello")
              Expect.equal result expected "Non-Empty Bulk String"
          }
          test "Parses NULL Array" {
              let input = "*-1\r\n"
              let result = Message.parseMessage input
              let expected = Array None
              Expect.equal result expected "NULL Array"
          }
          test "Parses Empty Array" {
              let input = "*0\r\n"
              let result = Message.parseMessage input
              let expected = Array(Some [])
              Expect.equal result expected "Empty Array"
          }
          test "Parses Array" {
              let input = "*2\r\n$5\r\nhello\r\n$5\r\nworld\r\n"
              let result = Message.parseMessage input
              let expected = Array(Some [ BulkString(Some "hello"); BulkString(Some "world") ])
              Expect.equal result expected "Non Empty Array"
          }
          test "Parses Nested Array" {
              let input = "*2\r\n*3\r\n:1\r\n:2\r\n:3\r\n*2\r\n+Hello\r\n-World\r\n"
              let result = Message.parseMessage input
              let expected = Array(Some [ Array(Some [ Integer 1; Integer 2; Integer 3 ]); Array(Some [ SimpleString "Hello"; Error "World" ]) ])
              Expect.equal result expected "Nested Array"
          }]
