module Converter

open Newtonsoft.Json

type FsDocWithType<'doctype>() = 
    inherit Davenport.Fsharp.Infrastructure.FsDoc<'doctype>()

    member val Type: string = "" with get, set

/// The Fable JsonConverter uses a cache, so it's best to just instantiate it once.
let private fableConverter = Fable.JsonConverter()

// We don't want to tie the type converter to a single generic type as it will ideally be parsing multiple
// different types (different types stored in the same database and queried with views relying on those types).
// Off the top of my head I'm envisioning giving this converter a list of [typeString, type, typeString -> type] tuple. 
// The big question is how to convert a result (e.g. list of those types given above) into a union type which can be returned.
// Maybe a second list of [UnionType, obj -> UnionType] tuples? The first list of tuples converts the individual docs, the second
// list of tuples transforms the docs into union types? Return an option to skip that transformation?

type FsTypeConverter(idField: string, revField: string) = 
    inherit JsonConverter()

    member __.CustomConverter = fableConverter

    override x.CanConvert objectType = true 