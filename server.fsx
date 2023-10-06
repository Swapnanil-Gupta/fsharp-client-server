open System
open System.Net
open System.Net.Sockets
open System.Text
open System.Collections.Generic

///////// GLOBAL VARIABLES ////////////
// this is the port that the server will listen to
// change if needed
let port = 3000 
// to maintain the client count
let mutable clientCount = 0
// to maintain the client connections and their id
let connectedClientsMap = Dictionary<TcpClient, int>()
// to cache the streams for all the connected clients
let clientStreamsMap = Dictionary<TcpClient, NetworkStream>()
////////// END GLOBAL VARIABLES ////////////


///////// UTILITY FUNCTIONS ///////////////
// takes an array of integers as input performs addtion
let performAddition (operands: int array) =
    Array.sum operands

// takes an array of integers as input performs subtraction
let performSubtraction (operands: int array) =
    Array.reduce (-) operands

// takes an array of integers as input performs multiplication
let performMultiplication (operands: int array) =
    Array.fold (*) 1 operands

// common method to send the data to client
let sendDataToClient (stream: NetworkStream, data: string) =
    let data = Encoding.ASCII.GetBytes(data)
    stream.Write(data, 0, data.Length)
    // adding this for readability purpose in command line
    printfn "---------------------------------------------" 

let receiveDataFromClient (stream: NetworkStream) =
    let buffer = Array.zeroCreate<byte> 1024
    let bytesRead = stream.Read(buffer, 0, buffer.Length)
    let response = Encoding.ASCII.GetString(buffer, 0, bytesRead)
    response
////////// END UTILITY FUNCTIONS ///////////////

// This is where the main business logic is
let handleClient (client: TcpClient) =
    async {
        try
            let stream = clientStreamsMap.[client]
            
            // if client coming for the first this block will execute 
            // and add the entry to our connected map by assigning an Id to it
            if not (connectedClientsMap.ContainsKey(client)) then
                clientCount <- clientCount + 1
                printfn "Connection created for client %d" clientCount
                printfn "Sending Hello to client %d" clientCount
                sendDataToClient (stream, "Hello!")
                connectedClientsMap.[client] <- clientCount
            
            let clientNo = connectedClientsMap.[client]
            while client.Connected do
                // receive data from the connected client
                let receivedData = receiveDataFromClient stream
                printfn "Request received from client %d and data: %s" clientNo receivedData

                if receivedData = "bye" || receivedData = "" then
                    // client sent a bye command, so send -5 to the client
                    // and close its connection
                    printfn "Closing the connnection for client %d" clientNo
                    sendDataToClient (stream, "-5")
                    connectedClientsMap.Remove(client) |> ignore
                    clientStreamsMap.Remove(client) |> ignore
                    stream.Close()
                    client.Close()
                else if receivedData = "terminate" then
                    // client sent a terminate command
                    // so send -5 to all connected client and terminate all connections
                    printfn "Terminating all the connections for the clients"
                    for client in connectedClientsMap.Keys do
                        try
                            let clientNumber = connectedClientsMap.[client]
                            let clientStream = clientStreamsMap.[client]
                            sendDataToClient (clientStream, "-5")
                            printfn "Closing the connection for client %d" clientNumber
                            clientStream.Close()
                            client.Close()
                        with
                        | ex ->
                            printfn "Error closing client connection: %s" ex.Message

                    // clear the cache and exit the program
                    connectedClientsMap.Clear()
                    clientStreamsMap.Clear()
                    printfn "Terminating the server"
                    Environment.Exit(0)
                else
                    // split the data received data from client by space
                    // and remove empty entries
                    let request = receivedData.Split([|' '|], StringSplitOptions.RemoveEmptyEntries) |> Array.map (fun s -> s.Trim())
                    let operandCount = request.Length - 1
                    let operator = request.[0]
                    let mutable finalResponse = ""

                    // if the operator is supported then perform the operation
                    if operator = "add" || operator = "subtract" || operator = "multiply" then
                        printfn "Performing: %s operation" operator
                        
                        // parse the input operands
                        let mutable operands = Array.empty<int>
                        for item in Seq.skip 1 request do
                            // all the operands should be integers
                            if System.Int32.TryParse(item) |> fst then
                                let parsedItem = int item
                                operands <- Array.append operands [| parsedItem |]
                            // if any of the operands is not an integer
                            // return -4 to the client
                            else 
                                finalResponse <- "-4"

                        // verify the count of operands
                        if operandCount < 2 then
                            printfn "Number of inputs sent by client %d is less than two" clientNo
                            finalResponse <- "-2"
                        else if operandCount > 4 then
                            printfn "Number of inputs sent by client %d is more than four" clientNo
                            finalResponse <- "-3"
                        else if finalResponse <> "-4" then
                            // we have between 2 to 4 valid operands
                            // proceed to perform the operation
                            let res =
                                if operator = "add" then
                                    performAddition operands
                                elif operator = "subtract" then
                                    performSubtraction operands
                                else 
                                    performMultiplication operands
                            
                            // convert the result to string so that it can be sent to the client
                            finalResponse <- res.ToString()
                    // if operator is not supported return -1 to the client
                    else 
                        finalResponse <- "-1"
                        printfn "Incorrect operation command by client %d" clientNo

                    // finally send the response back to the client
                    printfn "Sending data to client %d and the data: %s" clientNo finalResponse
                    sendDataToClient (stream, finalResponse)
        with
        // If the server terminates all the connections, the other threads
        // waiting for client inputs will face IO exceptions
        | :? IO.IOException ->
            client.Close()
        | ex ->
            printfn "Error processing client request: %s" ex.Message
    }

let startServer (port: int) =
    let listener = new TcpListener(IPAddress.Loopback, port)
    listener.Start()
    printfn "Server is running and listening on port %d" port
    //adding this for readability purpose in command line
    printfn "---------------------------------------------" 
    try
        while true do
            let client = listener.AcceptTcpClient()
            // get the stream for the client and cache it
            let stream = client.GetStream()
            clientStreamsMap.[client] <- stream
            handleClient client |> Async.Start
    with
    | ex ->
        printfn "Error processing client request: %s" ex.Message

startServer 3000