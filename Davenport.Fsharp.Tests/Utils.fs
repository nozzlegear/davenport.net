module Utils
open Expecto.Tests

module Expect = 
    let satisfies message fn value = 
        match fn value with 
        | true -> ()
        | false -> failtestf "%s. Expected value would satisfy function, but it did not." message
    let notNullOrEmpty message s = 
        match System.String.IsNullOrEmpty s with 
        | false -> ()
        | true -> failtestf "%s. Expected string to not be null or empty, but it was." message

module Async = 
    let Map fn task = async {
        let! result = task 

        return fn result
    }

    let Bind fn task = async {
        let! result = task

        return! fn result
    }