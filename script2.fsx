open JsonSchemaProvider

[<Literal>]
let schema1 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "integer",
        }
    },
    "required": ["values"]
}"""

type ProvidedType1 = JsonSchemaProvider<schema=schema1>

let value1 = ProvidedType1.Create(values = 1)
printfn "integer %O" ((value1.values.GetType()))


[<Literal>]
let schema2 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "array",
            "items": {
                "type": "array",
                "items": {"type": "string"}
            }
        }
    },
    "required": ["values"]
}"""

type ProvidedType2 = JsonSchemaProvider<schema=schema2>

let value2 = ProvidedType2.Create([ [ "a"; "b" ] ])
printfn "array of array of string %O" ((value2.values.GetType()))

[<Literal>]
let schema3 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "string",
        }
    },
    "required": ["values"]
}"""

type ProvidedType3 = JsonSchemaProvider<schema=schema3>

let value3 = ProvidedType3.Create(values = "aa")
printfn "string %O" ((value3.values.GetType()))

[<Literal>]
let schema4 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "boolean",
        }
    },
    "required": ["values"]
}"""

type ProvidedType4 = JsonSchemaProvider<schema=schema4>

let value4 = ProvidedType4.Create(values = true)
printfn "bool %O" ((value4.values.GetType()))

[<Literal>]
let schema5 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "number",
        }
    },
    "required": ["values"]
}"""

type ProvidedType5 = JsonSchemaProvider<schema=schema5>

let value5 = ProvidedType5.Create(values = 1.5)
printfn "float %O" (value5.values.GetType())

[<Literal>]
let schema6 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "object",
            "properties": {
                "a": {"type": "integer"},
                "b": {"type": "string"}
            },
            "required": ["a", "b"]
        }
    },
    "required": ["values"]
}"""

type ProvidedType6 = JsonSchemaProvider<schema=schema6>

let value6 =
    ProvidedType6.Create(values = ProvidedType6.valuesObj.Create(a = 1, b = "b"))

printfn "object %O" (value6.values)

// let value61 = ProvidedType6.valuesObj.Create(a=1, b="b")
// printfn "object %O" (value61.b)
// required:
// int, string, float, bool, object prop
// array of int, string, float, bool, object prop
// array of array of int, string, float, bool, object prop
// object prop with array of int, array of array of object


// Optional


[<Literal>]
let schema7 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "integer"
        }
    }
}"""

type ProvidedType7 = JsonSchemaProvider<schema=schema7>

let value7 = ProvidedType7.Create(values = 8)
printfn "none %O" (value7.values)

[<Literal>]
let schema8 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "string"
        }
    }
}"""

type ProvidedType8 = JsonSchemaProvider<schema=schema8>

let value8 = ProvidedType8.Create(values = "text")
printfn "optional string %O" (value8.values)


[<Literal>]
let schema9 =
    """
{
    "type": "object",
    "properties": {
        "values": {
            "type": "array",
            "items": {"type": "string"}
        }
    }
}"""

type ProvidedType9 = JsonSchemaProvider<schema=schema9>

let value9 = ProvidedType9.Create(values = [ "a"; "3" ])
printfn "optional string array %O" (value9.values)