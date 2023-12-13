﻿// Copyright (c) 2023 Florian Lorenzen

// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the “Software”), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:

// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

namespace JsonSchemaProvider.DesignTime

open System.IO
open System.Reflection
open FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open NJsonSchema
open FSharp.Data
open JsonSchemaProvider

[<TypeProvider>]
type JsonSchemaProviderImpl(config: TypeProviderConfig) as this =
    inherit
        TypeProviderForNamespaces(
            config,
            assemblyReplacementMap = [ ("JsonSchemaProvider.DesignTime", "JsonSchemaProvider.Runtime") ],
            addDefaultProbingLocation = true
        )

    let namespaceName = "JsonSchemaProvider"
    let thisAssembly = Assembly.GetExecutingAssembly()

    let staticParams =
        [ ProvidedStaticParameter("schema", typeof<string>, "")
          ProvidedStaticParameter("schemaFile", typeof<string>, "") ]

    let baseTy = typeof<NullableJsonValue>

    let jsonSchemaTy =
        ProvidedTypeDefinition(thisAssembly, namespaceName, "JsonSchemaProvider", baseType = Some baseTy)

    let rec determineReturnType
        (name: string)
        (item: JsonSchema)
        (schema: JsonSchema)
        (ty: ProvidedTypeDefinition)
        (isRequired: bool)
        (propType: JsonObjectType)
        =
        match propType with
        | JsonObjectType.String -> if isRequired then typeof<string> else typeof<string option>
        | JsonObjectType.Boolean -> if isRequired then typeof<bool> else typeof<bool option>
        | JsonObjectType.Integer -> if isRequired then typeof<int> else typeof<int option>
        | JsonObjectType.Number -> if isRequired then typeof<float> else typeof<float option>
        | JsonObjectType.Array ->
            let elementTy = determineReturnType name item.Item item ty true item.Type

            let list = typedefof<list<_>>
            list.MakeGenericType(elementTy)
        | JsonObjectType.Object ->
            let innerTy =
                ProvidedTypeDefinition(thisAssembly, namespaceName, name + "Obj", baseType = Some baseTy)

            generatePropertiesAndCreateForObject innerTy schema |> ignore

            ty.AddMember(innerTy)

            if isRequired then
                innerTy
            else
                let opt = typedefof<option<_>>
                opt.MakeGenericType(innerTy)
        | _ -> failwithf "Unsupported type %O" propType

    // and fromJsonVal (name: string) (schema: JsonSchema) =
    //     fun (args: Expr list) ->
    //         let fromNullable = <@@ fun (j:NullableJsonValue) -> j.JsonVal[name] @@>
    //         let selected = Expr.Application fromNullable args[0]
    //         let convert =
    //             match schema with
    //             | JsonObjectType.Integer -> <@@ fun (j:JsonValue) -> j.AsInteger() @@>
    //             | JsonExtensionObject.Array ->

    and fromJsonVal (returnType: System.Type) (name: string) =
        fun (args: Expr list) ->
            let fromNullable = <@@ fun (j: NullableJsonValue) -> j.JsonVal[name] @@>
            let selected = Expr.Application(fromNullable, args[0])

            let rec c (ty: System.Type) =
                printfn "type %O" ty
                if ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<_ list> then
                    let elemTy = ty.GenericTypeArguments[0]
                    let fSharpCore = typeof<List<_>>.Assembly

                    let listModuleType =
                        fSharpCore.GetTypes() |> Array.find (fun ty -> ty.Name = "ListModule")

                    let miOfArray =
                        listModuleType.GetMethods()
                        |> Array.find (fun methodInfo -> methodInfo.Name = "OfArray")
                        |> fun genericMethodInfo -> genericMethodInfo.MakeGenericMethod(typeof<JsonValue>)

                    let miMap =
                        listModuleType.GetMethods()
                        |> Array.find (fun methodInfo -> methodInfo.Name = "Map")
                        |> fun genericMethodInfo -> genericMethodInfo.MakeGenericMethod(typeof<JsonValue>, elemTy)
                    printfn "Method Info map: %O" miMap
                    //                              let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]

                    //   jsonVal.AsArray()
                    //   |> List.ofArray
                    //   |> List.map (fun jsonVal -> jsonVal.AsInteger())
                    let miAsArray =
                        match <@@ JsonValue.Array([||]).AsArray() @@> with
                        | Call(_, mi, _) -> mi
                        | _ -> failwith "Unexepcted expression"

                    let variable = Var("j", typeof<JsonValue>)
                    printfn "ggg"
                    let conv = c elemTy
                    printfn "COPNV %O" conv
                    for p in miMap.GetParameters() do
                        printfn "PARS %O" p    
                    printfn "RET %O" (miMap.ReturnType)
                    let ex =
                        Expr.Lambda(
                            variable,
                            Expr.Call(
                                miMap,
                                [ conv
                                  Expr.Call(miOfArray, [ Expr.Call(miAsArray, [Expr.Var(variable)]) ]) ]
                            )
                        )
                    printfn "ex %O" ex
                    ex
                    let x = JsonValue.Array([|JsonValue.Array([|JsonValue.String("a"); JsonValue.String("b")|])|])
                    let x1 = List.map (fun (j1:JsonValue) -> List.map (fun (j2: JsonValue) -> j2.AsString()) (List.ofArray (j1.AsArray()))) (List.ofArray (x.AsArray()))
                    ex
                //<@@ fun (j:JsonValue) -> List.map %%(c elemTy) (List.ofArray (j.AsArray()))@@>
                
                elif ty = typeof<string> then
                    printfn "return for string"
                    <@@ fun (j: JsonValue) -> j.AsString() @@>
                elif ty = typeof<int> then
                    <@@ fun (j: JsonValue) -> j.AsInteger() @@>
                elif ty = typeof<float> then
                    <@@ fun (j: JsonValue) -> j.AsFloat() @@>
                elif ty = typeof<bool> then
                    <@@ fun (j: JsonValue) -> j.AsBoolean() @@>
                else
                    <@@ fun (j: JsonValue) -> j @@>
            
            printf "ff"
            let f = c returnType
            printfn "f %O" f
            let ex = Expr.Application(f, selected)
            printf "%O" ex
            ex


    and generatePropertiesAndCreateForObject (ty: ProvidedTypeDefinition) (schema: JsonSchema) =
        let properties = schema.Properties
        let requiredProperties = schema.RequiredProperties

        let parametersForCreate =
            [ for prop in properties do
                  let name = prop.Key
                  let propType = prop.Value.Type
                  let isRequired = requiredProperties.Contains(name)

                  let returnType =
                      determineReturnType name prop.Value.Item prop.Value ty isRequired propType

                  let property =
                      ProvidedProperty(
                          propertyName = name,
                          propertyType = returnType,
                          getterCode =
                              //   if isRequired then
                              fromJsonVal returnType name
                      //   else
                      //       fun args -> Expr.Value("1")
                      //   match propType with
                      //   | JsonObjectType.String ->
                      //       fun args ->
                      //           <@@
                      //               let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]
                      //               jsonVal.AsString()
                      //           @@>
                      //   | JsonObjectType.Boolean ->
                      //       fun args ->
                      //           <@@
                      //               let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]
                      //               jsonVal.AsBoolean()
                      //           @@>
                      //   | JsonObjectType.Integer ->
                      //       fun args ->
                      //           <@@
                      //               let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]
                      //               jsonVal.AsInteger()
                      //           @@>
                      //   | JsonObjectType.Number ->
                      //       fun args ->
                      //           <@@
                      //               let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]
                      //               jsonVal.AsFloat()
                      //           @@>
                      //   | JsonObjectType.Array ->
                      //       match prop.Value.Item.Type with
                      //       | JsonObjectType.String ->
                      //           fun args ->
                      //               <@@
                      //                   let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]

                      //                   jsonVal.AsArray()
                      //                   |> List.ofArray
                      //                   |> List.map (fun jsonVal -> jsonVal.AsString())
                      //               @@>
                      //       | JsonObjectType.Boolean ->
                      //           fun args ->
                      //               <@@
                      //                   let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]

                      //                   jsonVal.AsArray()
                      //                   |> List.ofArray
                      //                   |> List.map (fun jsonVal -> jsonVal.AsBoolean())
                      //               @@>
                      //       | JsonObjectType.Integer ->
                      //           fun args ->
                      //               <@@
                      //                   let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]

                      //                   jsonVal.AsArray()
                      //                   |> List.ofArray
                      //                   |> List.map (fun jsonVal -> jsonVal.AsInteger())
                      //               @@>
                      //       | JsonObjectType.Number ->
                      //           fun args ->
                      //               <@@
                      //                   let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]

                      //                   jsonVal.AsArray()
                      //                   |> List.ofArray
                      //                   |> List.map (fun jsonVal -> jsonVal.AsFloat())
                      //               @@>
                      //       | JsonObjectType.Object ->
                      //           fun args ->
                      //               <@@
                      //                   let jsonVal = (%%args[0]: NullableJsonValue).JsonVal[name]
                      //                   jsonVal.AsArray() |> List.ofArray
                      //               @@>
                      //       | _ -> failwithf "Unsupported type %O" propType
                      //   | JsonObjectType.Object ->
                      //       fun args -> <@@ (%%args[0]: NullableJsonValue).JsonVal[name] @@>
                      //   | _ -> failwithf "Unsupported type %O" propType

                      // Only for debug
                      //   else
                      //       match propType with
                      //       | JsonObjectType.String ->
                      //           fun args ->
                      //               <@@
                      //                   let maybeJsonVal =
                      //                       (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                   maybeJsonVal
                      //                   |> Option.map (fun (jsonVal: JsonValue) -> jsonVal.AsString())
                      //               @@>
                      //       | JsonObjectType.Boolean ->
                      //           fun args ->
                      //               <@@
                      //                   let maybeJsonVal =
                      //                       (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                   maybeJsonVal
                      //                   |> Option.map (fun (jsonVal: JsonValue) -> jsonVal.AsBoolean())
                      //               @@>
                      //       | JsonObjectType.Integer ->
                      //           fun args ->
                      //               <@@
                      //                   let maybeJsonVal =
                      //                       (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                   maybeJsonVal
                      //                   |> Option.map (fun (jsonVal: JsonValue) -> jsonVal.AsInteger())
                      //               @@>
                      //       | JsonObjectType.Number ->
                      //           fun args ->
                      //               <@@
                      //                   let maybeJsonVal =
                      //                       (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                   maybeJsonVal |> Option.map (fun (jsonVal: JsonValue) -> jsonVal.AsFloat())
                      //               @@>
                      //       | JsonObjectType.Array ->
                      //           // TODO: Use Expr to and build a proper conversion from the array's element type that also allows nested types
                      //           match prop.Value.Item.Type with
                      //           | JsonObjectType.String ->
                      //               fun args ->
                      //                   <@@
                      //                       let maybeJsonVal =
                      //                           (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                       match maybeJsonVal with
                      //                       | None -> List.empty
                      //                       | Some(jsonVal) ->
                      //                           jsonVal.AsArray()
                      //                           |> List.ofArray
                      //                           |> List.map (fun jsonVal -> jsonVal.AsString())
                      //                   @@>
                      //           | JsonObjectType.Boolean ->
                      //               fun args ->
                      //                   <@@
                      //                       let maybeJsonVal =
                      //                           (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                       match maybeJsonVal with
                      //                       | None -> List.empty
                      //                       | Some(jsonVal) ->
                      //                           jsonVal.AsArray()
                      //                           |> List.ofArray
                      //                           |> List.map (fun jsonVal -> jsonVal.AsBoolean())
                      //                   @@>
                      //           | JsonObjectType.Integer ->
                      //               fun args ->
                      //                   <@@
                      //                       let maybeJsonVal =
                      //                           (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                       match maybeJsonVal with
                      //                       | None -> List.empty
                      //                       | Some(jsonVal) ->
                      //                           jsonVal.AsArray()
                      //                           |> List.ofArray
                      //                           |> List.map (fun jsonVal -> jsonVal.AsInteger())
                      //                   @@>
                      //           | JsonObjectType.Number ->
                      //               fun args ->
                      //                   <@@
                      //                       let maybeJsonVal =
                      //                           (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                       match maybeJsonVal with
                      //                       | None -> List.empty
                      //                       | Some(jsonVal) ->
                      //                           jsonVal.AsArray()
                      //                           |> List.ofArray
                      //                           |> List.map (fun jsonVal -> jsonVal.AsFloat())
                      //                   @@>
                      //           | JsonObjectType.Object ->
                      //               fun args ->
                      //                   <@@
                      //                       let maybeJsonVal =
                      //                           (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name)

                      //                       match maybeJsonVal with
                      //                       | None -> List.empty
                      //                       | Some(jsonVal) -> jsonVal.AsArray() |> List.ofArray

                      //                   @@>
                      //           | _ -> failwithf "Unsupported type %O" propType
                      //       | JsonObjectType.Object ->
                      //           fun args -> <@@ (%%args[0]: NullableJsonValue).JsonVal.TryGetProperty(name) @@>
                      //       | _ -> failwithf "Unsupported type %O" propType
                      )

                  ty.AddMember(property)

                  yield
                      if isRequired then
                          (ProvidedParameter(name, returnType, false), propType, prop.Value, isRequired)
                      else
                          let returnTypeWithoutOption = returnType.GenericTypeArguments[0]

                          let (nullableReturnType, defaultValue) =
                              if returnTypeWithoutOption.IsValueType then
                                  ((typedefof<System.Nullable<_>>).MakeGenericType([| returnTypeWithoutOption |]),
                                   System.Nullable() :> obj)
                              else
                                  (returnTypeWithoutOption, null :> obj)

                          (ProvidedParameter(name, nullableReturnType, false, defaultValue),
                           propType,
                           prop.Value,
                           isRequired) ]

        let create =
            let processArgs (args: Quotations.Expr list) =
                let ucArray =
                    FSharpType.GetUnionCases(typeof<JsonValue>)
                    |> Array.find (fun uc -> uc.Name = "Array")

                let fSharpCore = typeof<List<_>>.Assembly

                let listModuleType =
                    fSharpCore.GetTypes() |> Array.find (fun ty -> ty.Name = "ListModule")

                let miToArray (fromType: System.Type) =
                    listModuleType.GetMethods()
                    |> Array.find (fun methodInfo -> methodInfo.Name = "ToArray")
                    |> fun genericMethodInfo -> genericMethodInfo.MakeGenericMethod(fromType)

                let arrayModuleType =
                    fSharpCore.GetTypes() |> Array.find (fun ty -> ty.Name = "ArrayModule")

                let miMap (toType: System.Type) =
                    arrayModuleType.GetMethods()
                    |> Array.find (fun methodInfo -> methodInfo.Name = "Map")
                    |> fun genericMethodInfo -> genericMethodInfo.MakeGenericMethod(toType, typeof<JsonValue>)

                let rec toJsonValue (parameterType: System.Type) =
                    if
                        parameterType.IsGenericType
                        && parameterType.GetGenericTypeDefinition() = typedefof<_ list>
                    then
                        let elemType = parameterType.GetGenericArguments()[0]
                        // [["a"; "b"]] -> JsonValue.Array([|JsonValue.Array([|JsonValue.String("a"); JsonValue.String("b")|])|])

                        // fun x -> Array.map (fun x -> JsonValue.String(x)) (List.toArray x)

                        // fun x -> Array.map (fun x -> Array.map (fun x -> JsonValue.String(x)) (List.toArray x)) (List.toArray x)

                        // fun x -> List.toArray<innerTy> ()
                        let ucArray =
                            FSharpType.GetUnionCases(typeof<JsonValue>)
                            |> Array.find (fun uc -> uc.Name = "Array")

                        let x = System.Guid.NewGuid().ToString()
                        let variable = Var(x, parameterType)

                        Expr.Lambda(
                            variable,
                            Expr.NewUnionCase(
                                ucArray,
                                [ Expr.Call(
                                      miMap elemType,
                                      [ toJsonValue elemType; Expr.Call(miToArray elemType, [ Expr.Var(variable) ]) ]
                                  ) ]
                            )
                        )
                    elif parameterType = typeof<string> then
                        <@@ fun (x: string) -> JsonValue.String(x) @@>
                    else
                        <@@ fun (x: NullableJsonValue) -> x.JsonVal @@>


                [ for (arg, (parameter, propType, propValue, isRequired)) in List.zip args parametersForCreate do
                      let name = parameter.Name

                      yield
                          (match (propType, isRequired) with
                           | (_, true) ->
                               let lam = toJsonValue parameter.ParameterType

                               Expr.NewArray(
                                   typeof<string * JsonValue>,
                                   [ Expr.NewTuple([ <@@ name @@>; Expr.Application(lam, arg) ]) ]
                               )
                           //    | (JsonObjectType.String, true) ->
                           //        let ucString =
                           //            FSharpType.GetUnionCases(typeof<JsonValue>)
                           //            |> Array.find (fun uc -> uc.Name = "String")

                           //        Expr.NewArray(
                           //            typeof<string * JsonValue>,
                           //            [ Expr.NewTuple([ <@@ name @@>; Expr.NewUnionCase(ucString, [ arg ]) ]) ]
                           //        )
                           //    //<@@ [| (name, JsonValue.String(%%arg: string)) |] @@>
                           //    | (JsonObjectType.String, false) ->
                           //        <@@
                           //            match %%arg: string with
                           //            | null -> [||]
                           //            | value -> [| (name, JsonValue.String(value)) |]
                           //        @@>
                           //    | (JsonObjectType.Integer, true) ->
                           //        <@@ [| (name, JsonValue.Float((%%arg: int) |> float)) |] @@>
                           //    | (JsonObjectType.Integer, false) ->
                           //        <@@
                           //            if (%%arg: System.Nullable<int>).HasValue then
                           //                [| (name, JsonValue.Float((%%arg: System.Nullable<int>).Value)) |]
                           //            else
                           //                [||]
                           //        @@>
                           //    | (JsonObjectType.Number, true) -> <@@ [| (name, JsonValue.Float(%%arg: float)) |] @@>
                           //    | (JsonObjectType.Number, false) ->
                           //        <@@
                           //            if (%%arg: System.Nullable<float>).HasValue then
                           //                [| (name, JsonValue.Float((%%arg: System.Nullable<float>).Value)) |]
                           //            else
                           //                [||]
                           //        @@>
                           //    | (JsonObjectType.Array, true) ->

                           //        let (convertToJsonValue, toType) =
                           //            match propValue.Item.Type with
                           //            | JsonObjectType.Number -> (<@@ fun num -> JsonValue.Float(num) @@>, typeof<float>) // TODO: build proper conversion from the array's element type that also allows nesting
                           //            | JsonObjectType.Integer -> (<@@ fun num -> JsonValue.Number(num) @@>, typeof<int>)

                           //        let ucArray =
                           //            FSharpType.GetUnionCases(typeof<JsonValue>)
                           //            |> Array.find (fun uc -> uc.Name = "Array")

                           //        let fSharpCore = typeof<List<_>>.Assembly

                           //        let listModuleType =
                           //            fSharpCore.GetTypes() |> Array.find (fun ty -> ty.Name = "ListModule")

                           //        let miToArray =
                           //            listModuleType.GetMethods()
                           //            |> Array.find (fun methodInfo -> methodInfo.Name = "ToArray")
                           //            |> fun genericMethodInfo -> genericMethodInfo.MakeGenericMethod(toType)

                           //        let arrayModuleType =
                           //            fSharpCore.GetTypes() |> Array.find (fun ty -> ty.Name = "ArrayModule")

                           //        let miMap =
                           //            arrayModuleType.GetMethods()
                           //            |> Array.find (fun methodInfo -> methodInfo.Name = "Map")
                           //            |> fun genericMethodInfo ->
                           //                genericMethodInfo.MakeGenericMethod(toType, typeof<JsonValue>)

                           //        Expr.NewArray(
                           //            typeof<string * JsonValue>,
                           //            [ Expr.NewTuple(
                           //                  [ <@@ name @@>
                           //                    Expr.NewUnionCase(
                           //                        ucArray,
                           //                        [ Expr.Call(miMap, [ convertToJsonValue; Expr.Call(miToArray, [ arg ]) ]) ]
                           //                    ) ]
                           //              ) ]
                           //        )
                           //    // this works:
                           //    //    <@@
                           //    //        [| (name, JsonValue.Array((%%arg: List<float>) |> List.toArray |> Array.map (%%conv))) |]
                           //    //    @@>

                           //    | (JsonObjectType.Object, true) -> <@@ [| (name, (%%arg: NullableJsonValue).JsonVal) |] @@>
                           //    | (JsonObjectType.Object, false) ->
                           //        <@@
                           //            match %%arg: NullableJsonValue with
                           //            | null -> [||]
                           //            | jVal -> [| (name, jVal.JsonVal) |]
                           //        @@>
                           | (jsonObjectType, _) -> failwithf "Unsupported type %O" jsonObjectType) ]

            ProvidedMethod(
                methodName = "Create",
                parameters = (parametersForCreate |> List.map (fun (p, _, _, _) -> p)),
                returnType = ty,
                isStatic = true,
                invokeCode =
                    fun args11 ->
                        let schemaSource = schema.ToJson()
                        let schemaHashCode = schemaSource.GetHashCode()

                        <@@
                            let record =
                                NullableJsonValue(
                                    JsonValue.Record(
                                        Array.concat (
                                            (%%(Quotations.Expr.NewArray(
                                                typeof<(string * JsonValue)[]>,
                                                processArgs args11
                                            )))
                                            : (string * JsonValue)[][]
                                        )
                                    )
                                )

                            let recordSource = record.ToString()

                            let schema = SchemaCache.retrieveSchema schemaHashCode schemaSource

                            let validationErrors = schema.Validate(recordSource)

                            if Seq.isEmpty validationErrors then
                                record
                            else
                                let message =
                                    validationErrors
                                    |> Seq.map (fun validationError -> validationError.ToString())
                                    |> fun msgs ->
                                        System.String.Join(", ", msgs) |> sprintf "JSON Schema validation failed: %s"

                                raise (System.ArgumentException(message, recordSource))
                        @@>
            )

        ty.AddMember(create)
        ty
    do
        jsonSchemaTy.DefineStaticParameters(
            parameters = staticParams,
            instantiationFunction =
                fun typeName parameterValues ->
                    match parameterValues with
                    | [| :? string as schemaSource; :? string as schemaFile |] ->
                        if schemaSource = "" && schemaFile = "" || schemaSource <> "" && schemaFile <> "" then
                            failwith "Only one of schema or schemaFile must be set."

                        let schemaString =
                            if schemaSource <> "" then
                                schemaSource
                            else
                                File.ReadAllText(schemaFile)

                        let schema = SchemaCache.parseSchema schemaString
                        let schemaHashCode = schemaString.GetHashCode()

                        let ty =
                            ProvidedTypeDefinition(thisAssembly, namespaceName, typeName, baseType = Some baseTy)

                        if schema.Type <> JsonObjectType.Object then
                            failwith "Only object supported"

                        generatePropertiesAndCreateForObject ty schema |> ignore

                        let parse =
                            ProvidedMethod(
                                methodName = "Parse",
                                parameters = [ ProvidedParameter("json", typeof<string>) ],
                                returnType = ty,
                                isStatic = true,
                                invokeCode =
                                    fun args ->
                                        <@@
                                            let schema = SchemaCache.retrieveSchema schemaHashCode schemaString

                                            let validationErrors = schema.Validate((%%args[0]): string)

                                            if Seq.isEmpty validationErrors then
                                                NullableJsonValue(JsonValue.Parse(%%args[0]))
                                            else
                                                let message =
                                                    validationErrors
                                                    |> Seq.map (fun validationError -> validationError.ToString())
                                                    |> fun msgs ->
                                                        System.String.Join(", ", msgs)
                                                        |> sprintf "JSON Schema validation failed: %s"

                                                raise (System.ArgumentException(message, ((%%args[0]): string)))
                                        @@>
                            )

                        ty.AddMember(parse)

                        ty
                    | paramValues -> failwithf "Unexpected parameter values %O" paramValues
        )

    do this.AddNamespace(namespaceName, [ jsonSchemaTy ])
