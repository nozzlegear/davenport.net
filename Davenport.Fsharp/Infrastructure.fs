module Davenport.Fsharp.Infrastructure

open Newtonsoft.Json
open Newtonsoft.Json.Linq

/// Translates the C# PostPutCopyResponse, which contains a nullable book Ok prop, to F# removing the nullable bool.
type FsPostPutCopyResponse = {
    Id: string
    Rev: string
    Ok: bool
}

/// This type is combined with the custom json converter to allow consumers of this package to pass any F# record type to Davenport without turning their records into classes that inherit couchdoc.
type FsDoc<'doctype>() =
    inherit Davenport.Entities.CouchDoc()
    member val Data: 'doctype option = None with get,set

type FsConverter<'doctype>(idField: string, revField: string, customConverter: JsonConverter option) =
    inherit JsonConverter()

    member __.CustomConverter = Option.defaultWith (fun _ -> Fable.JsonConverter() :> JsonConverter) customConverter

    override x.CanConvert objectType =
        // This converter will convert anything that is an FsDoc or anything that can be handled by Fable.JsonConverter
        objectType = typeof<FsDoc<'doctype>> || x.CustomConverter.CanConvert objectType

    override x.ReadJson(reader: JsonReader, objectType: System.Type, existingValue: obj, serializer: JsonSerializer) =
        if objectType <> typeof<FsDoc<'doctype>>
        then x.CustomConverter.ReadJson(reader, objectType, existingValue, serializer)
        else

        let j = JObject.Load reader
        let id: JToken option = Option.ofObj j.["_id"]
        let rev: JToken option = Option.ofObj j.["_rev"]

        id
        |> Option.iter (fun value ->
            // Rename _id to idField
            if idField <> "_id" then
                j.Remove "_id" |> ignore
                j.Add(idField, value)
        )

        rev
        |> Option.iter (fun value ->
            // Rename _rev to revField
            if revField <> "_rev" then
                j.Remove "_rev" |> ignore
                j.Add(revField, value)
        )

        let data = j.ToObject<'doctype> serializer
        let output = FsDoc<'doctype>()

        output.Id <-
            id
            |> Option.bind (fun id -> id.Value<string>() |> Some)
            |> Option.defaultValue ""
        output.Rev <-
            rev
            |> Option.bind (fun rev -> rev.Value<string>() |> Some)
            |> Option.defaultValue ""
        output.Data <- Some data

        output :> obj

    override x.WriteJson(writer: JsonWriter, objValue: obj, serializer: JsonSerializer) =
        // If the value is not an FsDoc use the Fable.JsonConverter to serialize it.
        if objValue.GetType() <> typeof<FsDoc<'doctype>>
        then x.CustomConverter.WriteJson(writer, objValue, serializer)
        else

        writer.WriteStartObject()

        let doc = objValue :?> FsDoc<'doctype>
        let docType = typeof<'doctype>

        // Load the data object into a JObject
        let j = Option.get doc.Data |> JObject.FromObject

        // Find the data object's id and rev fields.
        // A JObject field will be null if it doesn't exist, but will return a JToken with Null value if the field does exist and it's null.
        let id =
            if isNull j.[idField]
            then sprintf "Id field '%s' was not found on type %s." idField docType.FullName |> System.ArgumentException |> raise
            else j.[idField]

        let rev =
            if isNull j.[revField]
            then sprintf "Rev field '%s' was not found on type %s." revField docType.FullName |> System.ArgumentException |> raise
            else j.[revField]

        // Write the _id and _rev values if they aren't null or empty. Writing either one when it isn't intended can make CouchDB throw an error.
        [id, "_id"; rev, "_rev"]
        |> Seq.iter (fun (token, name) ->
            let value = token.Value<string>()

            if System.String.IsNullOrEmpty value |> not then
                writer.WritePropertyName name
                writer.WriteValue value
        )

        // Merge the FsDoc's data property with the doc being written so they're at the same level.
        Seq.cast<JProperty> j
        |> Seq.filter (fun prop -> prop.Name <> idField && prop.Name <> revField)
        |> Seq.iter (fun prop -> prop.WriteTo(writer, [|x.CustomConverter|]))

        writer.WriteEndObject()
