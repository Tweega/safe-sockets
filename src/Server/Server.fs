namespace SafeSockets

open FSharp.Control.Tasks.V2
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Saturn
open Shared
open System.IO

/// Provides some simple functions over the ISocketHub interface.
module Channel =
    
    open Thoth.Json.Net

    /// Sends a message to a specific client by their socket ID.
    let sendMessage (hub:Channels.ISocketHub) socketId (payload:WebSocketServerMessage) = task {
        let payload = Encode.Auto.toString(0, payload)
        do! hub.SendMessageToClient "/channel" socketId "" payload
    }

    /// Sends a message to all connected clients.
    let broadcastMessage (hub:Channels.ISocketHub) (payload:WebSocketServerMessage) = task {
        let payload = Encode.Auto.toString(0, payload)
        do! hub.SendMessageToClients "/channel" "" payload
    }

    /// Sets up the channel to listen to clients.
    let channel = channel {
        join (fun ctx socketId ->
            task {
                ctx.GetLogger().LogInformation("Client has connected. They've been assigned socket Id: {socketId}", socketId)
                return Channels.Ok
            })
        handle "" (fun ctx _ci (message: Channels.Message<WebSocketClientMessage>) ->
            task {
                let hub = ctx.GetService<Channels.ISocketHub>()                
                
                // Here we handle any websocket client messages in a type-safe manner
                match message.Payload with
                | TextMessage message ->
                    ctx.GetLogger().LogInformation("lalalal: {message}", message)
                    let message = sprintf "Websocket message: %s" message
                    do! broadcastMessage hub (BroadcastMessage {| Time = System.DateTime.UtcNow; Message = message |})
            })

        not_found_handler (fun ctx _ci (message: Channels.Message<obj>) ->
            task {
                ctx.GetLogger().LogInformation("Nicht gefunden")
            }
        )
    }


module Server =

    open Fable.Remoting.Server
    open Fable.Remoting.Giraffe
    open Saturn

    open Shared

    type Storage () =
        let todos = ResizeArray<_>()

        member __.GetTodos () =
            List.ofSeq todos

        member __.AddTodo (todo: Todo) =
            if Todo.isValid todo.Description then
                todos.Add todo
                Ok ()
            else Error "Invalid todo"

    let storage = Storage()

    storage.AddTodo(Todo.create "Create new SAFE project") |> ignore
    storage.AddTodo(Todo.create "Write your app") |> ignore
    storage.AddTodo(Todo.create "Ship it !!!") |> ignore

    let todosApi =
        { getTodos = fun () -> async { return storage.GetTodos() }
          addTodo =
            fun todo -> async {
                match storage.AddTodo todo with
                | Ok () -> return todo
                | Error e -> return failwith e
            } }

    let webApp =
        Remoting.createApi()
        |> Remoting.withRouteBuilder Route.builder
        |> Remoting.fromValue todosApi
        |> Remoting.buildHttpHandler

    let app =
        application {
            url "http://0.0.0.0:8085"
            use_router webApp
            memory_cache
            use_static "public"
            use_json_serializer(Thoth.Json.Giraffe.ThothSerializer())
            use_gzip
            add_channel "/channel" Channel.channel

        }

    run app
