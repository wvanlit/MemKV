namespace MemKV.Protocol

type DataType =
    | SimpleString of string
    | Error of string
    | Integer of int
    | BulkString of string option
    | Array of DataType list

module MessageParser =
    let parse (data: string) : DataType =
       let prefix = data[0]
       
       Error($"Could not parse: {data}")