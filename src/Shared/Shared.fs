namespace Shared

open System

type Todo =
    { Id : Guid
      Description : string }

module Todo =
    let isValid (description: string) =
        String.IsNullOrWhiteSpace description |> not

    let create (description: string) =
        { Id = Guid.NewGuid()
          Description = description }

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type ITodosApi =
    { getTodos : unit -> Async<Todo list>
      addTodo : Todo -> Async<Todo> }

 
type DownstreamMessage = 
    {| Time : DateTime; Message : string |}

type UpstreamMessage = 
    {| Payload : string |}

// Add more messages that can go from server -> client here...
type WebSocketServerMessage =
    | BroadcastMessage of DownstreamMessage

// Add more message that can go from client -> server here...
type WebSocketClientMessage =
    | TextMessage of string