﻿module Brahma.FSharp.OpenCL.Full

open NUnit.Framework
open System.IO
open Brahma.Samples
open OpenCL.Net
open Brahma.OpenCL
open Brahma.FSharp.OpenCL.Wrapper
open System
open System.Reflection
open Microsoft.FSharp.Quotations


[<TestFixture>]
type Translator() =
    let defaultInArrayLength = 4
    let intInArr = [|0..defaultInArrayLength-1|]
    let float32Arr = Array.init defaultInArrayLength (fun i -> float32 i)
    let _1d = new _1D(defaultInArrayLength, 1)
    let deviceType = Cl.DeviceType.Default
    let platformName = "*"

    let provider =
        try  ComputeProvider.Create(platformName, deviceType)
        with
        | ex -> failwith ex.Message

    let checkResult command =
        let kernel,kernelPrepareF, kernelRunF = provider.Compile command                
        let commandQueue = new CommandQueue(provider, provider.Devices |> Seq.head)
        let check (outArray:array<'a>) (expected:array<'a>) =        
            let cq = commandQueue.Add(kernelRunF()).Finish()
            let r = Array.zeroCreate expected.Length
            let cq2 = commandQueue.Add(outArray.ToHost(kernel,r)).Finish()
            commandQueue.Dispose()
            Assert.AreEqual(expected, r)
            (kernel :> ICLKernel).CloseAllBuffers()
        kernelPrepareF,check

    [<Test>]
    member this.``Array item set``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    buf.[0] <- 1
            @>

        let run,check = checkResult command
        run _1d intInArr        
        check intInArr [|1;1;2;3|]
                

    [<Test>]
    member this.Binding() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    let x = 1
                    buf.[0] <- x
            @>
        
        let run,check = checkResult command
        run _1d intInArr        
        check intInArr [|1;1;2;3|]

    [<Test>]
    member this.``Binop plus``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    buf.[0] <- 1 + 2
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|3;1;2;3|]

    [<Test>]
    member this.``If Then``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    if 0 = 2 then buf.[0] <- 1
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|0;1;2;3|]

    [<Test>]
    member this.``If Then Else``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    if 0 = 2 then buf.[0] <- 1 else buf.[0] <- 2
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|2;1;2;3|]

    [<Test>]
    member this.``For Integer Loop``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    for i in 1..3 do buf.[i] <- 0
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|0;0;0;0|]

    [<Test>]
    member this.``Sequential bindings``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    let x = 1
                    let y = x + 1
                    buf.[0] <- y
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|2;1;2;3|]

    [<Test>]
    member this.``Binding in IF.``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    if 2 = 0
                    then                        
                        let x = 1
                        buf.[0] <- x
                    else
                        let i = 2
                        buf.[0] <- i
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|2;1;2;3|]

    [<Test>]
    member this.``Binding in FOR.``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) -> 
                    for i in 0..3 do
                        let x = i * i
                        buf.[0] <- x
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|9;1;2;3|] 

    [<Test>]
    member this.``WHILE loop simple test.``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) ->
                 while buf.[0] < 5 do
                     buf.[0] <- buf.[0] + 1
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|5;1;2;3|]

    [<Test>]
    member this.``WHILE in FOR.``() = 
        let command = 
            <@
                fun (range:_1D) (buf:array<int>) ->
                 for i in 0..3 do
                     while buf.[i] < 10 do
                         buf.[i] <- buf.[i] * buf.[i] + 1
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|26;26;26;10|]

    [<Test>]
    member this.``Binding in WHILE.``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) ->
                    while buf.[0] < 5 do
                        let x = buf.[0] + 1
                        buf.[0] <- x * x
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|25;1;2;3|]

    [<Test>]
    member this.``Simple 1D.``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) ->
                    let i = range.GlobalID0
                    buf.[i] <- i + i
            @>
        
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|0;2;4;6|]

    [<Test>]
    member this.``Simple 1D with copy.``() = 
        let command = 
            <@ 
                fun (range:_1D) (inBuf:array<int>) (outBuf:array<int>) ->
                    let i = range.GlobalID0
                    outBuf.[i] <- inBuf.[i]
            @>
        
        let run,check = checkResult command
        let outA = [|0;0;0;0|]
        run _1d intInArr outA        
        check outA [|0;1;2;3|]

    [<Test>]
    member this.``Simple 1D float.``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<float32>) ->
                    let i = range.GlobalID0
                    buf.[i] <- buf.[i] * buf.[i]
            @>
        
        let run,check = checkResult command
        run _1d float32Arr        
        check float32Arr [|0.0f;1.0f;4.0f;9.0f|]

    [<Test>]
    member this.``Math sin``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<float32>) ->
                    let i = range.GlobalID0
                    buf.[i] <- float32(System.Math.Sin (float buf.[i]))
            @>
        
        let run,check = checkResult command
        let inA = [|0.0f;1.0f;2.0f;3.0f|]
        run _1d inA
        check inA [|0.0f; 0.841471f; 0.9092974f; 0.14112f|]        

    [<Test>]
    member this.``Int as arg``() = 
        let command = 
            <@ 
                fun (range:_1D) x (buf:array<int>) ->
                    let i = range.GlobalID0
                    buf.[i] <- x + x
            @>
        let run,check = checkResult command
        run _1d 2 intInArr        
        check intInArr [|4;4;4;4|]

    [<Test>]
    member this.``Sequential commands over single buffer``() = 
        let command = 
            <@ 
                fun (range:_1D) i x (buf:array<int>) ->
                    buf.[i] <- x + x
            @>        
        let kernel,kernelPrepareF, kernelRunF = provider.Compile command
        kernelPrepareF _1d 0 2 intInArr
        let commandQueue = new CommandQueue(provider, provider.Devices |> Seq.head)        
        let _ = commandQueue.Add(kernelRunF())
        kernelPrepareF _1d 2 2 intInArr
        let _ = commandQueue.Add(kernelRunF())
        let _ = commandQueue.Finish()
        let r = Array.zeroCreate intInArr.Length
        let _ = commandQueue.Add(intInArr.ToHost(kernel, r)).Finish()
        commandQueue.Dispose()
        let expected = [|4;1;4;3|] 
        Assert.AreEqual(expected, r)
        (kernel :> ICLKernel).CloseAllBuffers()

    [<Test>]
    member this.``Sequential operations``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) ->
                    buf.[0] <- 2
                    buf.[1] <- 4
            @>
        let run,check = checkResult command
        run _1d intInArr
        check intInArr [|2;4;2;3|]