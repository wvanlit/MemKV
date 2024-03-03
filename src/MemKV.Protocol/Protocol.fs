namespace MemKV.Protocol

open System

type DataType =
    | SimpleString of string
    | Error of string
    | Integer of int
    | BulkString of string option
    | Array of DataType list option
    | Cmd of Command

and Command =
    | Ping of string option
    | Set of string * string
    | Get of string

module Message =
    let terminator = "\r\n"

    let rec private parse (data: string) : (DataType * string) =
        let prefix = data.[0]
        let nextTerminator = data.IndexOf(terminator)
        let msg = data.[1 .. nextTerminator - 1]
        let remainder = data.[nextTerminator + 2 ..]

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
                let nextData = data.[start .. endIdx - 1]
                let nextRemainder = if data.Length > endIdx then data.[endIdx + 2 ..] else ""
                (BulkString(Some nextData), nextRemainder)
        | '*' ->
            let count = int msg

            match count with
            | -1 -> (Array None, remainder)
            | count when count >= 0 ->
                let mutable elements = []
                let mutable nextData = remainder

                for _ in 1..count do
                    let (parsed, newRemainder) = parse nextData
                    elements <- elements @ [ parsed ]
                    nextData <- newRemainder

                (Array(Some elements), nextData)
            | _ -> failwith "todo"
        | _ -> (Error($"Could not parse: {data}"), remainder)

    let private parseCommand (data: DataType) : Command option =
        match data with
        | Array(Some [ BulkString(Some "PING") ]) -> (Ping None) |> Some
        | Array(Some [ BulkString(Some "PING"); BulkString(Some value) ]) -> Some(Ping(Some value))
        | Array(Some [ BulkString(Some "SET"); BulkString(Some key); BulkString(Some value) ]) -> Some(Set(key, value))
        | Array(Some [ BulkString(Some "GET"); BulkString(Some key) ]) -> Some(Get key)
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
