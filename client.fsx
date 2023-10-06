open System
open System.Net
open System.Net.Sockets
open System.Text

// this is the main function to start a client connection
let startClient (serverIP: string) (serverPort: int)= 
    // setup the ipaddress and the client
    let serverIPAddress = IPAddress.Parse(serverIP)
    let client = new TcpClient()
    // streams are expensive, so we will be reusing this
    use mutable stream: NetworkStream = null

    try
        // this variable lets up keep track of whether we are
        // still connected to the server or not
        let mutable serverRunning = true
        
        // connect to the client and get the IO stream
        client.Connect(serverIPAddress, serverPort)
        if client.Connected then
            stream  <- client.GetStream()
        else
           printfn "Could not connect to server. Closing.."
           client.Close()
           serverRunning <- false
        
        // prepare to read the hello response from the server
        let buffer: byte[] = Array.zeroCreate 1024
        let mutable bytesRead = 0
        let mutable helloReply = ""
        // read the hello response
        if serverRunning then
            bytesRead <- stream.Read(buffer, 0, buffer.Length)
            helloReply <- Encoding.ASCII.GetString(buffer, 0, bytesRead)
        
        // if the server has replied with hello, we can proceed
        if helloReply.Equals("Hello!") then 
            printfn "Recieved Hello from Server. Connected to server."
            while serverRunning do
                // Wait for the user to input a command
                printfn "--------------------------------------------"
                printfn "Enter Input:"
                let mutable message: string = ""
                // if the input is invalid, request for input again
                while message.Length = 0 do
                    message <- Console.ReadLine().Trim()
                    if message.Length = 0 then
                        printfn "Please enter a valid command"
                let bytes = Encoding.ASCII.GetBytes(message)
                
                // client-server connection could have been terminated
                // while we were waiting for input
                // so, check before sending the data
                if stream.CanWrite then
                    printfn "Sending command: %s" message
                    stream.Write(bytes, 0, bytes.Length)
                else
                    serverRunning <- false
                    printfn "Unable to write data to server. Disconnecting.."

                // prepare to receive the response from the client
                let buffer: byte[] = Array.zeroCreate 1024
                let mutable bytesRead = 0
                let mutable reply = ""
                if serverRunning then
                    // read the response from the server
                    // make sure the connection is still alive
                    // and the stream is readable before that
                    if stream.CanRead then
                        bytesRead <- stream.Read(buffer, 0, buffer.Length)
                    else
                        serverRunning <- false
                        printfn "Unable to read data from server. Disconecting.."
                    
                    // if the server sends empty response, disconnect
                    if serverRunning && bytesRead < 1 then
                        serverRunning <- false
                        printfn "No data recived from server. Disconnecting.."
                        
                    reply <- Encoding.ASCII.GetString(buffer, 0, bytesRead)
                    if bytesRead > 0 && not(reply.StartsWith("-")) then
                        printfn "Server response: %s" reply

                    // if response starts with - sign, it indicates an error
                    // so print the proper error
                    if reply.StartsWith("-") then
                        match reply with
                        | "-1" -> printfn "Incorrect operation command."
                        | "-2" -> printfn "Number of inputs is less than two."
                        | "-3" -> printfn "Number of inputs is more than four."
                        | "-4" -> printfn "One or more of the inputs contain non-numbers."
                        | "-5" -> printfn "Exit."
                        | _ -> printfn "Invalid error code: %s" reply
                        // The reply is a valid integer
                    if reply.StartsWith("-5") then
                        serverRunning <- false

            // at the end close all streams and connections
            stream.Close()   
            client.Close()
            Environment.Exit(0)

        // if the server did not reply with hello, we cant do anything
        // so close the connection and exit the program 
        elif serverRunning &&  not(helloReply.Equals("Hello!")) then
            printfn "Server did not respond Hello!. Disconnecting..."
            stream.Close()
            client.Close()
            Environment.Exit(0)
    with
    // Socket exceptions might occur if server closes the socket
    | :? SocketException as exp ->
        printfn "Error while connecting to server."
        client.Close()
        Environment.Exit(0)
    // IO Exceptions can occur if we try to use a connection that was
    // already terminated
    | :? IO.IOException as exp -> // Connection closed
        printfn "IO error occured, disconnecting from server."
        if stream <> null then
            stream.Close()
        if client.Connected then
            client.Close()
        Environment.Exit(0)

startClient "127.0.0.1" 3000