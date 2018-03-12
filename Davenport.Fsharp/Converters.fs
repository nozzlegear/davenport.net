module Davenport.Converters

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Types

// The Fable JsonConverter uses a cache, so it's best to just instantiate it once.
let fableConverter = Fable.JsonConverter()

let writeInsertedDocument (writer: JsonWriter) (doc: InsertedDocument<_>) (serializer: JsonSerializer) = 
    writer.WriteStartObject()

    let (idField, revField, typeName, docValue) = doc
    let docValueType = doc.GetType()
    let j = JObject.FromObject(docValue, serializer)

    typeName
    |> Option.iter (fun typeName ->
        writer.WritePropertyName "type"
        writer.WriteValue typeName
    )

    [
        j.[idField], idField, "_id", "Id";
        j.[revField], revField, "_rev", "Rev";
    ]
    |> Seq.iter (fun (token, givenFieldName, canonFieldName, readableFieldName) ->
        match isNull token with 
        | true -> 
            sprintf "%s field '%s' was not found on type %s." readableFieldName givenFieldName docValueType.FullName
            |> System.ArgumentException
            |> raise
        | false ->
            let value = j.[idField].Value<string>()

            if System.String.IsNullOrEmpty value |> not
            then 
                writer.WritePropertyName canonFieldName
                writer.WriteValue value
    )

    Seq.cast<JProperty> j 
    |> Seq.filter (fun prop -> prop.Name <> "type" && prop.Name <> idField && prop.Name <> revField)
    |> Seq.iter (fun prop -> prop.WriteTo(writer))       

    writer.WriteEndObject()

// 2018-03-10
// Thinking it would be really easy to handle the typenames by making the Davenport methods
// require a union type of InsertedDoc (typename: string * data: obj). We don't need to care about
// the deserialized data type because the dev will be able determine that using the type string.
// It could even use a union type for the data object, letting users pass a raw json string or an
// object that will be stringified by the converter.

// 2018-03-06 16:50 
// Current intended usage:
// singleDocType typeof<RandomType> "random-type"
// |> ...configure other client stuff
// |> get id rev
// |> fun (typeName, jtoken, defaultConverter) -> if typeName == "random-type" then jtoken.ToObject<RandomType>()
// for custom deserialization
// OR
// |> deserialize<RandomType> for default deserialization

// 2018-03-05 
// Trying to figure out how to get from this point, where we know the string type that was written by x.WriteJson,
// to converting the result to a union type.
// Maybe add a `multipleDocTypes` function to the library itself, and that function accepts a list of the TypeString * System.Type * Id field * Rev field
// to map all the types it will deal with. Then the original FsConverter receives those types (if it's not in multiple doc mode the converter still receives the
// list, just with one single element.)

type FsConverter() = 
    inherit JsonConverter()

    override __.CanConvert objectType = 
        [
            typeof<InsertedDocument<_>>
        ]
        |> Seq.contains objectType

    override x.ReadJson(reader: JsonReader, objectType: System.Type, existingValue: obj, serializer: JsonSerializer) = 
        existingValue
        // let j = JObject.Load reader 
        // let docTypeToken: JToken option = Option.ofObj j.["type"]

        // if JtokenOptionType docTypeToken |> x.CanConvertDirectly |> not 
        // then 
        //     x.CustomConverter.ReadJson(reader, objectType, existingValue, serializer)
        // else 

        // let docType = 
        //     JtokenOptionType docTypeToken 
        //     |> x.GetSupportedType
        //     |> Option.get

        // let id: JToken option = Option.ofObj j.["_id"]
        // let rev: JToken option = Option.ofObj j.["_rev"]

        // // Rename the _id and _rev fields to whatever the type expects them to be
        // match id, docType.idField with 
        // | None, _ -> ()
        // | _, None -> ()
        // | _, Some fieldName when fieldName = "_id" -> ()
        // | Some value, Some fieldName -> 
        //     j.Remove("_id") |> ignore 
        //     j.Add(fieldName, value)

        // match rev, docType.revField with 
        // | None, _ -> ()
        // | _, None -> ()
        // | _, Some fieldName when fieldName = "_rev" -> ()
        // | Some value, Some fieldName ->
        //     j.Remove("_rev") |> ignore
        //     j.Add(fieldName, value)

        // if objectType = typeof<Document> then 
        //     let output: CouchResult = docType.typeName, j

        //     output :> obj
        // else 
        //     j.ToObject(objectType)

    override __.WriteJson(writer: JsonWriter, objValue: obj, serializer: JsonSerializer) =
        match objValue with 
        | :? InsertedDocument<obj> as inserted -> writeInsertedDocument writer inserted serializer
        | _ -> failwithf "FsConverter.WriteJson: Unsupported object type %A." (objValue.GetType())