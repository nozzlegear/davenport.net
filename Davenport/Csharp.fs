namespace Davenport.Csharp.Types

    open Davenport.Types 
    open Davenport.Infrastructure
    open System
    open System.Collections.Generic
    open System.Linq.Expressions

    [<AbstractClass>]
    type CouchDoc() = 
        abstract member Id: string with get, set
        abstract member Rev: string with get, set

    type Revision(rev) = 
        new() = Revision("")
        member val Rev: string = rev with get, set

    type ListedRow<'doctype>(doc) = 
        member val Id = "" with get, set
        member val Key: obj = null with get, set
        member val Value: Revision option = None with get, set
        member val Doc: 'doctype option = doc with get, set

    type ListResponse<'doctype>() = 
        member val Offset = 0 with get, set
        member val TotalRows = 0 with get, set 
        member val Rows: IEnumerable<ListedRow<'doctype>> = Seq.empty with get, set
        member val DesignDocs: IEnumerable<ListedRow<obj>> = Seq.empty with get, set

    type ViewResponse<'value, 'doc>(key, value) = 
        member val Key: ViewKey = key with get, set 
        member val Value: 'value = value with get, set
        member val Doc: 'doc option = None with get, set

    type ViewConfig() = 
        member val Name: string = "" with get, set
        member val MapFunction: string = "" with get, set
        member val ReduceFunction: string = "" with get, set

    // type DesignDocument() = 
    //     inherit CouchDoc()
    //     override val Id = "" with get, set
    //     override val Rev = "" with get, set
    //     member val Views: IEnumerable<ViewConfig> = Seq.empty with get, set

    type ListOptions() = 
        member val Limit = System.Nullable<int>() with get, set
        member val Key: obj = null with get, set
        member val Keys: obj seq = Seq.empty with get, set
        member val StartKey: obj = null with get, set
        member val EndKey: obj = null with get, set
        member val InclusiveEnd = System.Nullable<bool>() with get, set
        member val Descending = System.Nullable<bool>() with get, set
        member val Skip = System.Nullable<int>() with get, set
        member val Group = System.Nullable<bool>() with get, set
        member val GroupLevel = System.Nullable<int>() with get, set

    type FindExpression() = 
        member val EqualTo: obj = null with get, set
        member val NotEqualTo: obj = null with get, set
        member val GreaterThan: obj = null with get, set
        member val GreaterThanOrEqualTo: obj = null with get, set
        member val LesserThan: obj = null with get, set
        member val LesserThanOrEqualTo: obj = null with get, set
        static member Create(expType: ExpressionType, value) = 
            let exp = FindExpression()

            match expType with 
            | ExpressionType.Equal -> 
                exp.EqualTo <- value 
            | ExpressionType.NotEqual ->
                exp.NotEqualTo <- value 
            | ExpressionType.GreaterThan ->
                exp.GreaterThan <- value 
            | ExpressionType.GreaterThanOrEqual ->
                exp.GreaterThanOrEqualTo <- value 
            | ExpressionType.LessThan -> 
                exp.LesserThan <- value
            | ExpressionType.LessThanOrEqual ->
                exp.LesserThanOrEqualTo <- value
            | ExpressionType.Or 
            | ExpressionType.OrElse ->
                "CouchDB's find method does not support || expressions. We recommend constructing a view instead."
                |> ArgumentException
                |> raise
            | _ -> 
                sprintf "Attempted to create a FindExpression with an unsupported ExpressionType: %A." expType
                |> ArgumentException 
                |> raise

            exp
            

    /// <remarks>
    /// SortingOrder is the same as SortOrder (F# version), but replicated here so the C# client can (theoretically) never need to open the Types module and pollute their scope with F# types.
    /// </remarks>
    type SortingOrder = 
        | Ascending 
        | Descending

    type Sorting(fieldName: string, order: SortingOrder) =
        member val FieldName = fieldName with get, set
        member val Order = order with get, set

    type UsableIndexType = 
        | FromDesignDoc
        | FromDesignDocAndIndex

    type UsableIndex (designDocId: string, indexName: string option) =
        new (designDocId: string, indexName: string) = UsableIndex(designDocId, Some indexName)
        member val DesignDocId = designDocId with get, set
        member val IndexName = (indexName |> Option.defaultValue "") with get, set

    type FindOptions(useIndex: UsableIndex option) = 
        new () = FindOptions(None)
        member val Fields: string list = [] with get, set
        member val SortBy: Sorting list = [] with get, set
        member val Limit = System.Nullable<int>() with get, set
        member val Skip = System.Nullable<int>() with get, set
        member val UseIndex: UsableIndex option = useIndex with get, set

    type TypeOfIndex = 
        | Json 

    type IndexOptions() = 
        member val DDoc = "" with get, set
        member val Name = "" with get, set 
        member val Type = TypeOfIndex.Json with get, set

    type Configuration(couchUrl: string, databaseName: string) = 
        member val CouchUrl = couchUrl with get, set
        member val DatabaseName = databaseName with get, set
        member val Username = "" with get, set
        member val Password = "" with get, set
        member val Converter: ICouchConverter option = None with get, set
        member val Warning: EventHandler<string> = null with get, set

namespace Davenport.Csharp
    open Davenport.Fsharp 
    open Davenport.Types 
    open Davenport.Infrastructure
    open System
    open System.Threading.Tasks
    open System.Collections.Generic
    open Davenport.Csharp.Types
    open System.Runtime.InteropServices

    module ExpressionParser = 
        open System.Linq.Expressions

        type Value = obj

        type IsPropName = bool

        type PropName = string 

        let private (|BinaryExp|_|) (exp: Expression<_>) = 
            match exp.Body with 
            | :? BinaryExpression as x -> Some x
            | _ -> None

        let invokeExpression(exp: Expression): Value = 
            Expression.Lambda(exp).Compile().DynamicInvoke()

        let getMemberValue(exp: MemberExpression): IsPropName * Value = 
            try 
                false, invokeExpression exp
            with 
            | _ -> true, exp.Member.Name :> obj

        let rec getExpressionValue(exp: Expression): IsPropName * Value = 
            match exp.NodeType with 
            | ExpressionType.Constant -> 
                let cnst = exp :?> ConstantExpression

                false, cnst.Value 
            | ExpressionType.MemberAccess -> 
                exp :?> MemberExpression 
                |> getMemberValue
            | ExpressionType.Convert -> 
                let unary = exp :?> UnaryExpression

                getExpressionValue unary.Operand
            | _ -> 
                sprintf "Expression type %A is invalid." exp.NodeType 
                |> ArgumentException
                |> raise

        let getExpressionParts(exp: BinaryExpression): PropName * Value = 
            let leftIsPropName, leftValue = getExpressionValue exp.Left 
            let _, rightValue = getExpressionValue exp.Right 

            match leftIsPropName with 
            | true -> leftValue :?> string, rightValue
            | false -> rightValue :?> string, leftValue

        let private partsToOperator (fn: obj -> FindOperator) exp =
            let propName, value = getExpressionParts exp 

            propName, fn value  

        let private (|EqualExpr|_|) (exp: BinaryExpression) = 
            match exp.NodeType with 
            | ExpressionType.Equal -> 
                exp 
                |> partsToOperator FindOperator.EqualTo 
                |> Some
            | _ -> None

        let private (|NotEqualExpr|_|) (exp: BinaryExpression) = 
            match exp.NodeType with 
            | ExpressionType.NotEqual -> 
                exp 
                |> partsToOperator FindOperator.NotEqualTo 
                |> Some
            | _ -> None 

        let private (|GreaterThanExpr|_|) (exp: BinaryExpression) = 
            match exp.NodeType with 
            | ExpressionType.GreaterThan -> 
                exp 
                |> partsToOperator FindOperator.GreaterThan
                |> Some
            | _ -> None

        let private (|GTEExpr|_|) (exp: BinaryExpression) = 
            match exp.NodeType with 
            | ExpressionType.GreaterThanOrEqual ->
                exp 
                |> partsToOperator FindOperator.GreaterThanOrEqualTo
                |> Some 
            | _ -> None

        let private (|LessThanExpr|_|) (exp: BinaryExpression) = 
            match exp.NodeType with 
            | ExpressionType.LessThan ->
                exp 
                |> partsToOperator FindOperator.LesserThan 
                |> Some
            | _ -> None

        let private (|LTEExpr|_|) (exp: BinaryExpression) = 
            match exp.NodeType with 
            | ExpressionType.LessThanOrEqual ->
                exp 
                |> partsToOperator FindOperator.LessThanOrEqualTo
                |> Some 
            | _ -> None

        let private (|OrExpr|_|) (exp: BinaryExpression) = 
            match exp.NodeType with 
            | ExpressionType.Or 
            | ExpressionType.OrElse -> Some exp 
            | _ -> None

        let parse<'doctype>(exp: Expression<Func<'doctype, bool>>): FindSelector = 
            match exp with 
            | BinaryExp exp -> 
                let propName, operator =
                    match exp with 
                    | EqualExpr x -> x
                    | NotEqualExpr x -> x 
                    | GreaterThanExpr x -> x
                    | GTEExpr x -> x 
                    | LessThanExpr x -> x
                    | LTEExpr x -> x 
                    | OrExpr _ ->
                        "CouchDB's find method does not support || expressions. We recommend constructing a view instead."
                        |> ArgumentException
                        |> raise
                    | _ -> 
                        sprintf "Davenport currently only supports == expressions. Type received: %A." exp.NodeType
                        |> ArgumentException 
                        |> raise

                Map.empty 
                |> Map.add propName [operator]
            | _ -> 
                "Invalid expression. Expression must be in the form of e.g. x => x.Foo == 5 and must use the document parameter passed in."
                |> ArgumentException 
                |> raise

    type Client<'doctype when 'doctype :> CouchDoc>(config: Configuration) = 
        let maybeAddConverter (jsonConverter: ICouchConverter option) (props: CouchProps) =
            match jsonConverter with 
            | None -> props
            | Some j -> props |> converter j

        let maybeAddWarning (evtHandler: EventHandler<string> option) (props: CouchProps) = 
            match evtHandler with 
            | None -> props 
            | Some handler -> props |> warning (fun s -> handler.Invoke(config, s))

        let typeName = 
            let t = typeof<'doctype>
            t.FullName

        let client = 
            config.CouchUrl
            |> database config.DatabaseName
            |> fun props -> match config.Username with | NotNullOrEmpty s -> username s props | _ -> props
            |> fun props -> match config.Password with | NotNullOrEmpty s -> password s props | _ -> props
            |> maybeAddConverter config.Converter
            |> maybeAddWarning (Option.ofObj config.Warning)
            |> mapFields (Map.empty |> Map.add typeName ("Id", "Rev"))

        let toDoc (d: Document) = d.To<'doctype>()

        let toObj (d: Document) = d.To<obj>()

        let toRevision (d: Document) = d.To<Revision>()

        let rec viewKeyToObj = function
            | ViewKey.Null -> null :> obj 
            | ViewKey.JToken k -> k.ToObject<obj>()
            | ViewKey.List k -> (k |> List.map viewKeyToObj) :> obj
            | k -> k :> obj 

        let listOptionsToFs (o: ListOptions) = 
            let descendingToOption (d: bool option) = 
                match d with 
                | Some true -> Some (ListOption.Direction SortOrder.Descending)
                | Some false -> Some (ListOption.Direction SortOrder.Ascending )
                | _ -> None

            [
                Option.ofNullable o.Limit |> Option.map ListOption.ListLimit
                Option.ofObj o.Key |> Option.map ListOption.Key 
                Option.ofSeq o.Keys |> Option.map (List.ofSeq >> ListOption.Keys)
                Option.ofObj o.StartKey |> Option.map ListOption.StartKey
                Option.ofObj o.EndKey |> Option.map ListOption.EndKey 
                Option.ofNullable o.InclusiveEnd |> Option.map ListOption.InclusiveEnd
                Option.ofNullable o.Descending |> descendingToOption
                Option.ofNullable o.Skip |> Option.map ListOption.Skip
                Option.ofNullable o.Group |> Option.map ListOption.Group 
                Option.ofNullable o.GroupLevel |> Option.map ListOption.GroupLevel
                // Do not include the Reduce option in this list, as the view function will figure that out instead.
            ]
            |> List.filter Option.isSome 
            |> List.map Option.get

        let findExpressionToFs (o: FindExpression) = 
            [
                Option.ofObj o.EqualTo |> Option.map FindOperator.EqualTo
                Option.ofObj o.NotEqualTo |> Option.map FindOperator.NotEqualTo
                Option.ofObj o.GreaterThan |> Option.map FindOperator.GreaterThan
                Option.ofObj o.GreaterThanOrEqualTo |> Option.map FindOperator.GreaterThanOrEqualTo
                Option.ofObj o.LesserThan |> Option.map FindOperator.LesserThan 
                Option.ofObj o.LesserThanOrEqualTo |> Option.map FindOperator.LessThanOrEqualTo
            ]
            |> List.filter Option.isSome
            |> List.map Option.get

        let usableIndexToFs (index: UsableIndex) = 
            match index.IndexName with 
            | NotNullOrEmpty indexName -> UseIndex.FromDesignDocAndIndex(index.DesignDocId, indexName)
            | _ -> UseIndex.FromDesignDoc index.DesignDocId

        let sortingToFs (sorts: Sorting list): Sort list = 
            sorts 
            |> List.map (fun sort -> Sort (sort.FieldName, match sort.Order with SortingOrder.Ascending -> SortOrder.Ascending | SortingOrder.Descending -> SortOrder.Descending))

        let findOptionsToFs (o: FindOptions): FindOption list = 
            [
                Option.ofSeq o.Fields |> Option.map FindOption.Fields
                Option.ofSeq o.SortBy |> Option.map (sortingToFs >> FindOption.SortBy)
                Option.ofNullable o.Limit |> Option.map FindOption.FindLimit 
                Option.ofNullable o.Skip |> Option.map FindOption.Skip 
                o.UseIndex |> Option.map (usableIndexToFs >> FindOption.UseIndex)
            ]
            |> List.filter Option.isSome 
            |> List.map Option.get

        let indexOptionsToFs (o: IndexOptions): IndexOption list = 
            let type' = 
                match o.Type with 
                | TypeOfIndex.Json -> IndexOption.Type IndexType.Json

            [
                Option.ofString o.DDoc |> Option.map IndexOption.DDoc 
                Option.ofString o.Name |> Option.map IndexOption.Name 
                type' |> Some
            ]
            |> List.filter Option.isSome 
            |> List.map Option.get

        let dictToMap fn (dict: Dictionary<'a, 'b>) = 
            dict 
            |> Seq.map (fun kvp -> kvp.Key, fn kvp.Value)
            |> Map.ofSeq

        let task = Async.StartAsTask

        new(couchUrl, databaseName) = Client(Configuration(couchUrl, databaseName))

        /// <remarks>
        /// Made this function into a class member, because the private class functions can't use generics -- they must be moved out of the class to the module instead.
        /// </remarks>
        member private __.ToListResponse<'d>(r: ViewResult): ListResponse<'d> = 
            let rows, designDocs = 
                r.Rows
                |> Seq.fold (fun (rows: ListedRow<'d> list, designDocs: ListedRow<obj> list) row -> 
                    match row.Id with 
                    | i when i.StartsWith "_design" -> 
                        let result = ListedRow<obj>(row.Doc |> Option.map toObj)
                        result.Id <- row.Id
                        result.Key <- viewKeyToObj row.Key
                        result.Value <- row.Value |> Option.map toRevision

                        rows, (designDocs@[result])
                    | _ -> 
                        let result = ListedRow<'d>(row.Doc |> Option.map (fun d -> d.To<'d>()))
                        result.Id <- row.Id 
                        result.Key <- viewKeyToObj row.Key 
                        result.Value <- row.Value |> Option.map toRevision

                        (rows@[result]), designDocs) 
                    ([], [])

            let resp = ListResponse<'d>()
            resp.TotalRows <- r.TotalRows 
            resp.Offset <- r.Offset
            resp.Rows <- Seq.ofList rows 
            resp.DesignDocs <- Seq.ofList designDocs
            resp

        member __.GetAsync(id: string, [<Optional; DefaultParameterValue(null)>] ?rev: string): Task<'doctype> =
            client 
            |> get id rev
            |> Async.Map toDoc
            |> task

        member __.FindByExpressionAsync (exp: Linq.Expressions.Expression<Func<'doctype, bool>>, [<Optional; DefaultParameterValue(null)>] ?options: FindOptions): Task<IEnumerable<'doctype>> = 
            let opts = 
                options 
                |> Option.map findOptionsToFs 
                |> Option.defaultValue []

            exp 
            |> ExpressionParser.parse 
            |> find opts 
            <| client 
            |> Async.MapSeq toDoc 
            |> task

        member __.FindBySelectorAsync (dict, [<Optional; DefaultParameterValue(null)>] ?options: FindOptions): Task<IEnumerable<'doctype>> = 
            let opts = 
                options 
                |> Option.map findOptionsToFs
                |> Option.defaultValue []

            dict 
            |> dictToMap findExpressionToFs 
            |> find opts 
            <| client 
            |> Async.MapSeq toDoc
            |> task

        member __.CountAsync(): Task<int> = 
            client 
            |> count 
            |> task

        member __.CountByExpressionAsync (exp: Linq.Expressions.Expression<Func<'doctype, bool>>): Task<int> = 
            exp 
            |> ExpressionParser.parse 
            |> countBySelector 
            <| client 
            |> task

        member __.CountBySelectorAsync (dict): Task<int> = 
            dict
            |> dictToMap findExpressionToFs 
            |> countBySelector 
            <| client
            |> task

        member __.ExistsAsync (id, [<Optional; DefaultParameterValue(null)>] ?rev: string): Task<bool> = 
            client 
            |> exists id rev
            |> task

        member __.ExistsBySelectorAsync (dict): Task<bool> = 
            dict 
            |> dictToMap findExpressionToFs
            |> existsBySelector
            <| client
            |> task

        member __.ExistsByExpressionAsync (exp: Linq.Expressions.Expression<Func<'doctype, bool>>): Task<bool> = 
            exp 
            |> ExpressionParser.parse
            |> existsBySelector
            <| client 
            |> task

        member x.ListWithDocsAsync ([<Optional; DefaultParameterValue(null)>] ?options: ListOptions): Task<ListResponse<'doctype>> =
            let opts = 
                options 
                |> Option.map listOptionsToFs
                |> Option.defaultValue []
                |> List.append [ListOption.IncludeDocs true]

            client 
            |> listAll opts
            |> Async.Map x.ToListResponse<'doctype>
            |> task

        member x.ListWithoutDocsAsync ([<Optional; DefaultParameterValue(null)>]?options: ListOptions): Task<ListResponse<obj>> = 
            let opts = 
                options 
                |> Option.map listOptionsToFs
                |> Option.defaultValue []
                |> List.append [ListOption.IncludeDocs false]

            client 
            |> listAll opts
            |> Async.Map x.ToListResponse<obj>
            |> task

        member __.CreateAsync (doc: 'doctype): Task<PostPutCopyResult> = 
            client 
            |> create (Some typeName, doc)
            |> task

        member __.UpdateAsync (id, doc: 'doctype, rev): Task<PostPutCopyResult> = 
            (Some typeName, doc)
            |> update id rev 
            <| client 
            |> task

        member __.CopyAsync (id, newId): Task<PostPutCopyResult> = 
            client 
            |> copy id newId
            |> task
        
        member __.DeleteAsync (id, rev): Task<unit> = 
            client
            |> delete id rev 
            |> task

        /// <summary>
        /// Queries a view. 
        /// NOTE: This function forces the `reduce` parameter to FALSE, i.e. it will NOT reduce. Use the `reduce` functions instead.
        /// </summary>
        member __.ViewAsync<'returnType, 'docType> (designDocName, viewName, [<Optional; DefaultParameterValue(null)>] ?options): Task<IEnumerable<ViewResponse<'returnType, 'docType>>> = 
            options
            |> Option.map listOptionsToFs
            |> Option.defaultValue []
            |> view designDocName viewName
            <| client
            |> Async.Map (fun result -> result.Rows)
            |> Async.MapSeq (fun row ->
                    let vr = ViewResponse<'returnType, 'docType>(row.Key, row.Value.Value.To<'returnType>())
                    vr.Doc <- row.Doc |> Option.map (fun d -> d.To<'docType>())
                    vr )
            |> task

        /// <summary>
        /// Queries a view and reduces it.
        /// NOTE: This function forces the `reduce` parameter to TRUE< i.e. will ALWAYS reduce. Use the `view` or function to query a view's docs instead.
        /// </summary>
        member __.ReduceAsync<'returnType> (designDocName, viewName, [<Optional; DefaultParameterValue(null)>] ?options): Task<'returnType> = 
            options 
            |> Option.map listOptionsToFs
            |> Option.defaultValue []
            |> reduce designDocName viewName 
            <| client 
            |> Async.Map (fun d -> d.To<'returnType>())
            |> task

        /// <summary>
        /// Inserts, updates or deletes multiple documents at the same time. 
        /// 
        /// Omitting the id property from a document will cause CouchDB to generate the id itself.
        /// 
        /// When updating a document, the `_rev` property is required.
        /// 
        /// To delete a document, set the `_deleted` property to `true`. 
        /// 
        /// Note that CouchDB will return in the response an id and revision for every document passed as content to a bulk insert, even for those that were just deleted.  
        /// 
        /// If the `_rev` does not match the current version of the document, then that particular document will not be saved and will be reported as a conflict, but this does not prevent other documents in the batch from being saved. 
        /// 
        /// If the new edits are *not* allowed (to push existing revisions instead of creating new ones) the response will not include entries for any of the successful revisions (since their rev IDs are already known to the sender), only for the ones that had errors. Also, the `"conflict"` error will never appear, since in this mode conflicts are allowed. 
        /// </summary>
        member __.BulkInsert (allowNewEdits: bool, docs: IEnumerable<'doctype>): Task<IEnumerable<BulkResult>> = 
            let mode = 
                match allowNewEdits with 
                | true -> BulkMode.AllowNewEdits
                | false -> BulkMode.NoNewEdits
            
            docs
            |> List.ofSeq
            |> List.map (fun d -> Some typeName, d)
            |> bulkInsert mode
            <| client
            |> Async.Map Seq.ofList
            |> task

        /// <summary>
        /// Creates a CouchDB database if it doesn't exist.
        /// </summary>
        member __.CreateDatabaseAsync(): Task<CreateResult> = 
            client 
            |> createDatabase 
            |> task

        /// <summary>
        /// Deletes the database. This cannot be undone!
        /// </summary>
        member __.DeleteDatabaseAsync(): Task<unit> = 
            client 
            |> deleteDatabase 
            |> task

        /// <summary>
        /// Creates the given design docs. This is a dumb function and will overwrite the data of any design doc that shares its id.
        /// </summary>
        member __.CreateOrUpdateDesignDocAsync (name: string, views: IEnumerable<ViewConfig>, [<Optional; DefaultParameterValue(null)>] ?rev: string): Task<unit> = 
            views 
            |> Seq.fold (fun views view -> views |> Map.add view.Name (view.MapFunction, Option.ofString view.ReduceFunction)) Map.empty
            |> DesignDoc.doc name
            |> createOrUpdateDesignDoc rev
            <| client 
            |> task

        member __.CreateIndexesAsync (indexes: IEnumerable<string>, [<Optional; DefaultParameterValue(null)>] ?options: IndexOptions): Task<IndexInsertResult> = 
            let opts = 
                options 
                |> Option.map indexOptionsToFs
                |> Option.defaultValue []

            client 
            |> createIndexes opts (List.ofSeq indexes)
            |> task

        member __.GetCouchVersion(): Task<string> = 
            client 
            |> getCouchVersion 
            |> task 

        member __.IsVersionTwoOrAbove(): Task<bool> = 
            client 
            |> isVersion2OrAbove 
            |> task