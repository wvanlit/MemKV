namespace MemKV.Protocol

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO

type Key = string

type DataType =
    | SimpleString of string
    | Error of string
    | Integer of int
    | BulkString of string option
    | Array of DataType list option
    | Cmd of Command

and Command =
    | Ping of string option
    | Set of Key * string
    | Get of Key
    | Exists of Key list
    | Delete of Key list
    | Increment of Key
    | Decrement of Key
    | Save

module Message =
    let terminator = "\r\n"

    let rec private parse (data: string) : DataType * string =
        let prefix = data[0]
        let nextTerminator = data.IndexOf(terminator)
        let msg = data[1 .. nextTerminator - 1]
        let remainder = data[nextTerminator + 2 ..]

        match prefix with
        | '+' -> (SimpleString msg, remainder)
        | '-' -> (Error msg, remainder)
        | ':' -> (Integer(int msg), remainder)
        | '$' ->
            let length = int msg

            if length = -1 then
                (BulkString None, remainder)
            else
                let start = nextTerminator + 2
                let endIdx = start + length
                let nextData = data[start .. endIdx - 1]
                let nextRemainder = if data.Length > endIdx then data[endIdx + 2 ..] else ""
                (BulkString(Some nextData), nextRemainder)
        | '*' ->
            let count = int msg

            match count with
            | -1 -> (Array None, remainder)
            | count when count >= 0 ->
                let mutable elements = []
                let mutable nextData = remainder

                for _ in 1..count do
                    let parsed, newRemainder = parse nextData
                    elements <- elements @ [ parsed ]
                    nextData <- newRemainder

                (Array(Some elements), nextData)
            | _ -> failwith "todo"
        | _ -> (Error($"Could not parse: {data}"), remainder)

    let private parseKeys (keys: DataType list) =
        List.map
            (fun key ->
                match key with
                | BulkString(Some s) -> s
                | _ -> failwith "Key should be a bulk string")
            keys

    let private parseCommand (data: DataType) : Command option =
        match data with
        | Array(Some [ BulkString(Some "PING") ]) -> (Ping None) |> Some
        | Array(Some [ BulkString(Some "PING"); BulkString(Some value) ]) -> Some(Ping(Some value))
        | Array(Some [ BulkString(Some "SET"); BulkString(Some key); BulkString(Some value) ]) -> Some(Set(key, value))
        | Array(Some [ BulkString(Some "GET"); BulkString(Some key) ]) -> Some(Get key)
        | Array(Some(BulkString(Some "EXISTS") :: keys)) -> keys |> parseKeys |> Exists |> Some
        | Array(Some(BulkString(Some "DEL") :: keys)) -> keys |> parseKeys |> Delete |> Some
        | Array(Some [ BulkString(Some "INCR"); BulkString(Some key) ]) -> Some(Increment key)
        | Array(Some [ BulkString(Some "DECR"); BulkString(Some key) ]) -> Some(Decrement key)
        | Array(Some [ BulkString(Some "SAVE") ]) -> Some Save
        | _ -> None

    let parseMessage (data: string) : DataType =
        match parse data with
        | msg, "" ->
            match parseCommand msg with
            | Some cmd -> Cmd cmd
            | None -> msg
        | _ -> Error $"Could not parse message: {data}"

    let rec serialize (msg: DataType) : string =
        match msg with
        | SimpleString s -> $"+{s}{terminator}"
        | Error e -> $"-{e}{terminator}"
        | Integer i -> $":{i}{terminator}"
        | BulkString None -> $"$-1{terminator}"
        | BulkString(Some s) -> $"${s.Length}{terminator}{s}{terminator}"
        | Array None -> $"*-1{terminator}"
        | Array(Some elements) ->
            let mutable result = $"*{elements.Length}{terminator}"

            for el in elements do
                result <- result + serialize el

            result
        | Cmd cmd -> failwith $"Cannot serialize command: %A{cmd}"

type DictionaryStore() =
    let store = ConcurrentDictionary<Key, string>()

    member this.Set(key: Key, value: string) = store[key] <- value

    member this.Get(key: Key) =
        match store.TryGetValue key with
        | true, value -> Some value
        | _ -> None

    member this.Exists(keys: Key list) =
        keys |> List.map (fun key -> store.ContainsKey key)

    member this.Delete(keys: Key list) =
        keys |> List.map (fun key -> store.Remove key) |> List.map fst

    member this.GetOrSetDefault(key: Key, fallback: string) =
        match store.TryGetValue key with
        | true, value -> value
        | _ ->
            store[key] <- fallback
            fallback


    member this.Increment(key: Key) =
        let v = this.GetOrSetDefault(key, "0")

        match Int32.TryParse v with
        | true, value ->
            store[key] <- (value + 1).ToString()
            Integer(value + 1)
        | _ -> Error $"Value \"{v}\" is not an integer"

    member this.Decrement(key: Key) =
        let v = this.GetOrSetDefault(key, "0")

        match Int32.TryParse v with
        | true, value ->
            store[key] <- (value - 1).ToString()
            Integer(value - 1)
        | _ -> Error $"Value \"{v}\" is not an integer"

    member this.Save() =
        File.WriteAllText(
            this.PathToStore,
            store |> Seq.map (fun kv -> $"{kv.Key}={kv.Value}") |> String.concat "\n")

    member this.Load() =
        if File.Exists(this.PathToStore) then
            let data = File.ReadAllText(this.PathToStore)
            let lines = data.Split('\n', StringSplitOptions.RemoveEmptyEntries)

            let entries =
                lines
                |> Array.map (_.Split('=', 2))
                |> Array.map (fun parts -> (parts.[0], parts.[1]))

            store.Clear()

            for key, value in entries do
                store[key] <- value

    member private this.PathToStore =
        Path.Combine(Directory.GetCurrentDirectory(), "store.db")
