---
title: Serialization in KEFCore
_description: Describes how works the serialization in Entity Framework Core provider for Apache Kafka™
---

# KEFCore: serialization

[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) shall convert the entities used within the model in something viable from the backend.
Each backend has its own schema to convert entities into something else; database providers converts entities into database schema or blob in Cosmos.

## Basic concepts

[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) shall convert the entities into record will be stored in the topics of Apache Kafka™ cluster.
The way the entities are converted shall follows a schema.
The current schema follows a JSON pattern and reports the information of each entity as:
- Primary Key:
  - Simple: if the Primary Key is a native type (e.g. int, long, double, and so on) the serialization is made using the Apache Kafka™ default serializer for that type
  - Complex: if the Primary Key is a complex type (e.g. int-int, int-long, int-string, and so on), Entity Framework reports it as an array of objects and the serialization is made using the configured serializer (default is a JSON serializer)

- Entity data: the Entity is managed, from [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/), as an array of objects associated to the properties of the Entity. 
  The schema of the Apache Kafka™ record value follows the code definition in `DefaultValueContainer<T>`. Below two examples:
  ```json
  {
    "EntityName": "MASES.EntityFrameworkCore.KNet.Test.Blog",
    "ClrType": "MASES.EntityFrameworkCore.KNet.Test.Blog",
    "Data": {
      "0": {
        "PropertyName": "BlogId",
        "ClrType": "System.Int32",
        "Value": 8
      },
      "1": {
        "PropertyName": "Rating",
        "ClrType": "System.Int32",
        "Value": 7
      },
      "2": {
        "PropertyName": "Url",
        "ClrType": "System.String",
        "Value": "http://blogs.msdn.com/adonet7"
      }
    }
  }
  ```

  ```json
  {
    "EntityName": "MASES.EntityFrameworkCore.KNet.Test.Post",
    "ClrType": "MASES.EntityFrameworkCore.KNet.Test.Post",
    "Data": {
      "0": {
        "PropertyName": "PostId",
        "ClrType": "System.Int32",
        "Value": 44
      },
      "1": {
        "PropertyName": "BlogId",
        "ClrType": "System.Int32",
        "Value": 44
      },
      "2": {
        "PropertyName": "Content",
        "ClrType": "System.String",
        "Value": "43"
      },
      "3": {
        "PropertyName": "Title",
        "ClrType": "System.String",
        "Value": "title"
      }
    }
  }
  ```

The equivalent JSON schema is not available till now.

### AVRO and Protobuf

Beside the JSON serializer there are both [AVRO](#avro-serialization) and [Protobuf](#protobuf-serialization) serializer.

### Default managed types

Each serializer supports the following list of types can be used to declare the fields of the entities in [Entity Framework Core](https://learn.microsoft.com/ef/core/):
- string
- Guid
- Guid?
- DateTime
- DateTime?
- DateTimeOffset
- DateTimeOffset?
- bool
- bool?
- char
- char?
- sbyte
- sbyte?
- byte
- byte?
- short
- short?
- ushort
- ushort?
- int
- int?
- uint
- uint?
- long
- long?
- ulong
- ulong?
- double
- double?
- float
- float?
- decimal
- decimal?
- byte[]

## Code and user override

The code is based on three elements shall be available to [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) in order to work:
- **ValueContainer type**: a type which encapsulate the Entity and stores needed information
- **Key SerDes**: the serializer of the Primary Key
- **ValueContainer SerDes**: the serializer of the ValueContainer

### Default types

[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) comes with some default values:
- **ValueContainer** class: KEFCore uses `DefaultValueContainer<T>` (i.e. `DefaultKEFCoreSerDes.DefaultValueContainer`) which stores the CLR type of Entity, the properties ordered by their index with associated CLT type, name and JSON serializaed value; the class is marked for JSON serialization and it is used from the **ValueContainer SerDes**;
- **Key SerDes** class: KEFCore uses `DefaultKEFCoreSerDes.Key.JsonRaw<T>` (i.e. `DefaultKEFCoreSerDes.DefaultKeySerialization`), the type automatically manages simple or complex Primary Key
- **ValueContainer SerDes** class: KEFCore uses `DefaultKEFCoreSerDes.ValueContainer.JsonRaw<>` (i.e. `DefaultKEFCoreSerDes.DefaultValueContainerSerialization`)

Both Key and ValueContainer SerDes come with two kind of data transfer mechanisms:
- Raw: it uses `byte` arrays for data transfer
- Buffered: they use `ByteBuffer` for data transfer

### User override

The default serialization can be overridden with user defined **ValueContainer** class, **Key SerDes** or **ValueContainer SerDes**.

#### **ValueContainer** class

A custom **ValueContainer** class must contains enough information and shall follow the following rules:
- must implements the [`IValueContainer<T>` interface](https://github.com/masesgroup/KEFCore/blob/master/src/net/KEFCore.SerDes/IValueContainer.cs)
- must be a generic type
- must have at least a default constructor and a constructor which accept six parameters in this order:
  1. `IValueContainerData` -> the reference containing support information and data
  6. `IComplexTypeConverterFactory?` -> an optional reference to the factory where the instance will retrieve specialized serializer, see [Complex type serialization](#complex-type-serialization)

An example snippet is the follow:

```C#
public class CustomValueContainer<TKey> : IValueContainer<TKey> where TKey : notnull
{
    /// <summary>
    /// Initialize a new instance of <see cref="CustomValueContainer{TKey}"/>
    /// </summary>
    /// <param name="valueContainerData">The <see cref="IValueContainerData"/> containing the information to prepare an instance of <see cref="DefaultValueContainer{TKey}"/></param>
    /// <param name="complexTypeFactory">The instance of <see cref="IComplexTypeConverterFactory"/> will manage strong type conversion</param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="CustomValueContainer{TKey}"/></remarks>
    public CustomValueContainer(IValueContainerData valueContainerData, IComplexTypeConverterFactory? complexTypeFactory = null)
    {
        // add specific logic
    }

    /// <summary>
    /// The Entity name of <see cref="IEntityType"/>
    /// </summary>
    public string EntityName { get; set; }
    /// <summary>
    /// The CLR <see cref="Type"/> of <see cref="IEntityType"/>
    /// </summary>
    public string ClrType { get; set; }
    /// <summary>
    /// Returns back the raw data associated to the Entity contained in <see cref="IValueContainer{T}"/> instance
    /// </summary>
    /// <param name="metadata">The requesting <see cref="IValueContainerMetadata"/> to get the data back, can <see langword="null"/> if not available</param>
    /// <param name="allPropertyValues">The array of object to be filled in with the data stored in the <see cref="IValueContainer{T}"/> instance for <paramref name="metadata"/></param>
    /// <param name="complexTypeFactory">The optional <see cref="IComplexTypeConverterFactory"/> instance to manage conversion of <see cref="IComplexType"/></param>
    public void GetData(IValueContainerMetadata metadata, ref object[] allPropertyValues, IComplexTypeConverterFactory? complexTypeFactory)
    {
        // add specific logic
    }
    /// <summary>
    /// Returns back a dictionary of properties (PropertyName, Value) associated to the Entity
    /// </summary>
    /// <param name="complexTypeHook">The optional <see cref="IComplexTypeConverterFactory"/> instance to manage conversion of <see cref="IComplexType"/></param>
    /// <returns>A dictionary of properties (PropertyName, Value) filled in with the data stored in the <see cref="IValueContainer{T}"/> instance</returns>
    public IDictionary<string, object?> GetProperties(IComplexTypeConverterFactory? complexTypeHook = null)
    {
        // add specific logic
    }
}
```

#### **Key SerDes** and **ValueContainer SerDes** class

A custom **Key SerDes** class shall follow the following rules:
- must implements the `ISerDes<T>` interface or extend `SerDes<T>`
- must be a generic type
- must have a parameterless constructor
- can store serialization information using Headers of Apache Kafka™ record (this information will be used from `EntityExtractor`)

An example snippet is the follow based on JSON serializer:

```C#
public class CustomKeySerDes<T> : SerDesRaw<T>
{
    readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(T).FullName!);
    readonly byte[] customSerDesName = Encoding.UTF8.GetBytes(typeof(CustomKeySerDes<>).FullName!);

    /// <inheritdoc cref="SerDes{T, TJVM}.Serialize(string, T)"/>
    public override byte[] Serialize(string topic, T data)
    {
        return SerializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="SerDes{T, TJVM}.SerializeWithHeaders(string, Headers, T)"/>
    public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
    {
        headers?.Add(KEFCoreSerDesNames.KeyTypeIdentifier, keyTypeName);
        headers?.Add(KEFCoreSerDesNames.KeySerializerIdentifier, customSerDesName);

        if (data == null) return null;

        var jsonStr = System.Text.Json.JsonSerializer.Serialize<T>(data);
        return Encoding.UTF8.GetBytes(jsonStr);
    }
    /// <inheritdoc cref="SerDes{T, TJVM}.Deserialize(string, TJVM)"/>
    public override T Deserialize(string topic, byte[] data)
    {
        return DeserializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="SerDes{T, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
    public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
    {
        if (data == null || data.Length == 0) return default;
        return System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
    }
}
```

```C#
public class CustomValueContainerSerDes<T> : SerDesRaw<T>
{
    readonly byte[] valueContainerSerDesName = Encoding.UTF8.GetBytes(typeof(CustomValueContainerSerDes<>).FullName!);
    readonly byte[] valueContainerName = null!;
    /// <summary>
    /// Default initializer
    /// </summary>
    public CustomValueContainerSerDes()
    {
        var tt = typeof(T);
        if (tt.IsGenericType)
        {
            var keyT = tt.GetGenericArguments();
            if (keyT.Length != 1) { throw new ArgumentException($"{typeof(T).Name} does not contains a single generic argument and cannot be used because it is not a valid ValueContainer type"); }
            var t = tt.GetGenericTypeDefinition();
            if (t.GetInterface(typeof(IValueContainer<>).Name) != null)
            {
                valueContainerName = Encoding.UTF8.GetBytes(t.FullName!);
                return;
            }
            else throw new ArgumentException($"{typeof(T).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
        }
        throw new ArgumentException($"{typeof(T).Name} is not a generic type and cannot be used as a valid ValueContainer type");
    }

    /// <inheritdoc cref="SerDes{T, TJVM}.Serialize(string, T)"/>
    public override byte[] Serialize(string topic, T data)
    {
        return SerializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="SerDes{T, TJVM}.SerializeWithHeaders(string, Headers, T)"/>
    public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
    {
        headers?.Add(KEFCoreSerDesNames.ValueContainerSerializerIdentifier, valueContainerSerDesName);
        headers?.Add(KEFCoreSerDesNames.ValueContainerIdentifier, valueContainerName);

        if (data == null) return null;

        var jsonStr = System.Text.Json.JsonSerializer.Serialize<T>(data);
        return Encoding.UTF8.GetBytes(jsonStr);
    }
    /// <inheritdoc cref="SerDes{T, TJVM}.Deserialize(string, TJVM)"/>
    public override T Deserialize(string topic, byte[] data)
    {
        return DeserializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="SerDes{T, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
    public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
    {
        if (data == null || data.Length == 0) return default;
        return System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
    }
}
```

### How to use custom types 

`KafkaDbContext` contains three properties can be used to override the default types:
- **KeySerializationType**: set the value of the **Key SerDes** type in the form `CustomSerDes<>`
- **ValueSerializationType**: set the value of the **ValueContainer SerDes** type in the form `CustomSerDes<>`
- **ValueContainerType**: set the value of the **ValueContainer** type in the form `CustomValueContainer<>`

> **IMPORTANT NOTE**: the type applied in the previous properties of `KafkaDbContext` shall be a generic type definition, [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) will apply the right generic type when needed.

## **Avro** serialization

With package [MASES.EntityFrameworkCore.KNet.Serialization.Avro](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Avro/) an user can choose two different Avro serializers:
The engine comes with two different encoders 
- Binary
- Json

Both Key and ValueContainer SerDes, Binary or Json, come with two kind of data transfer mechanisms:
- Raw: it uses `byte` arrays for data transfer
- Buffered: they use `ByteBuffer` for data transfer

### Avro schema

The following schema is the default used from the engine and can be registered in Apache Schema registry so other applications can use it to extract the data stored in the topics:

- Complex Primary Key schema:
  ```json
  {
  	"namespace": "MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage",
  	"type": "record",
  	"name": "AvroKeyContainer",
  	"doc": "Represents the storage container type to be used from KEFCore for keys",
  	"fields": [
  		{
  			"name": "PrimaryKey",
  			"doc": "Represents a primary key used from KEFCore",
  			"type": {
  				"type": "array",
  				"items": [
  					"null",
  					"boolean",
  					"int",
  					"long",
  					"float",
  					"double",
  					"string"
  				]
  			}
  		}
  	]
  }
  ```


- ValueContainer schema:
  ```json
  {
  	"namespace": "MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage",
  	"type": "record",
  	"name": "AvroValueContainer",
  	"doc": "Represents the storage container type to be used from KEFCore values",
  	"fields": [
  		{
  			"name": "EntityName",
  			"doc": "Represents the entity name of the Entity in the EF Core schema of the EntityType",
  			"type": "string"
  		},
  		{
  			"name": "ClrType",
  			"doc": "Represents the CLR type of the Entity in the EF Core schema of the EntityType",
  			"type": "string"
  		},
  		{
  			"name": "Data",
  			"type": {
  				"type": "array",
  				"items": {
  					"namespace": "MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage",
  					"type": "record",
  					"name": "PropertyDataRecord",
  					"doc": "Represents the single container for Entity properties stored in AvroValueContainer and used from KEFCore",
  					"fields": [
  						{
  							"name": "PropertyIndex",
  							"doc": "Represents the index of the property in the EF Core schema of the EntityType",
  							"type": "int"
  						},
  						{
  							"name": "PropertyName",
  							"doc": "Represents the name of the property in the EF Core schema of the EntityType",
  							"type": "string"
  						},
  						{
  							"name": "ManagedType",
  							"doc": "Represents the internal KEFCore type associated to the property in the EF Core schema of the EntityType",
  							"type": "int"
  						},
  						{
  							"name": "SupportNull",
  							"doc": "true if the ManagedType shall support null, e.g. Nullable type in .NET",
  							"type": "boolean"
  						},
  						{
  							"name": "ClrType",
  							"doc": "Represents the CLR type of the property in the EF Core schema of the EntityType",
  							"type": "string"
  						},
  						{
  							"name": "Value",
  							"type": [
  								"null",
  								"boolean",
  								"int",
  								"long",
  								"float",
  								"double",
  								"string"
  							]
  						}
  					]
  				}
  			}
  		}
  	]
  }
  ```
The extension converted this schema into code to speedup the exection of serialization/deserialization operations.

### How to use Avro 

`KafkaDbContext` contains three properties can be used to override the default types:
- **KeySerializationType**: set this value to `AvroKEFCoreSerDes.Key.BinaryRaw<>` or `AvroKEFCoreSerDes.Key.JsonRaw<>` or use `AvroKEFCoreSerDes.DefaultKeySerialization` (defaults to `AvroKEFCoreSerDes.Key.BinaryRaw<>`), both types automatically manages simple or complex Primary Key
- **ValueSerializationType**: set this value to `AvroKEFCoreSerDes.ValueContainer.BinaryRaw<>` or `AvroKEFCoreSerDes.ValueContainer.JsonRaw<>` or use `AvroKEFCoreSerDes.DefaultValueContainerSerialization` (defaults to `AvroKEFCoreSerDes.ValueContainer.BinaryRaw<>`)
- **ValueContainerType**: set this value to `AvroValueContainerRaw<>` or use `AvroKEFCoreSerDes.DefaultValueContainer`

An example is:

```C#
using (context = new BloggingContext()
{
    BootstrapServers = "KAFKA-SERVER:9092",
    ApplicationId = "MyAppid",
    DbName = "MyDBName",
    KeySerializationType = UseAvroBinary ? typeof(AvroKEFCoreSerDes.Key.BinaryRaw<>) : typeof(AvroKEFCoreSerDes.Key.JsonRaw<>),
    ValueContainerType = typeof(AvroValueContainer<>),
    ValueSerializationType = UseAvroBinary ? typeof(AvroKEFCoreSerDes.ValueContainer.BinaryRaw<>) : typeof(AvroKEFCoreSerDes.ValueContainer.JsonRaw<>),
})
{
	// execute stuff here
}
```

## **Protobuf** serialization

With package [MASES.EntityFrameworkCore.KNet.Serialization.Protobuf](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf/) an user can choose the Protobuf serializer.

Both Key and ValueContainer SerDes come with two kind of data transfer mechanisms:
- Raw: it uses `byte` arrays for data transfer
- Buffered: they use `ByteBuffer` for data transfer

### Protobuf schema

The following schema is the default used from the engine and can be registered in Apache Schema registry so other applications can use it to extract the data stored in the topics:

- Common multitype value:
  ```protobuf
  // [START declaration]
  syntax = "proto3";
  package storage;
  
  import "google/protobuf/struct.proto";
  import "google/protobuf/timestamp.proto";
  // [END declaration]
  
  // [START java_declaration]
  option java_multiple_files = true;
  option java_package = "mases.entityframeworkcore.knet.serialization.protobuf";
  option java_outer_classname = "GenericValue";
  // [END java_declaration]
  
  // [START csharp_declaration]
  option csharp_namespace = "MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage";
  // [END csharp_declaration]
  
  // [START messages]
  // Represent a System.Datetime
  message Datetime
  {
      // Represents a Timestamp value.
      google.protobuf.Timestamp datetime_value = 1;
      // Represents a Utc/Local value.
      bool utc_value = 2;
  }
  
  // Our address book file is just one of these.
  message GenericValue {
    // The kind of value.
    oneof kind {
      // Represents a null value.
      google.protobuf.NullValue null_value = 1;
      // Represents a boolean value.
      bool bool_value = 2;
      // Represents a char value.
      string char_value = 3;
      // Represents a int value.
      uint32 byte_value = 4;
      // Represents a int value.
      int32 sbyte_value = 5;
      // Represents a int value.
      int32 short_value = 6;
      // Represents a int value.
      uint32 ushort_value = 7;
      // Represents a int value.
      int32 int_value = 8;
      // Represents a int value.
      uint32 uint_value = 9;
      // Represents a long value.
      int64 long_value = 10;
      // Represents a long value.
      uint64 ulong_value = 11;
      // Represents a float value.
      float float_value = 12;
      // Represents a double value.
      double double_value = 13;
      // Represents a string value.
      string string_value = 14;
      // Represents a Guid value.
      bytes guid_value = 15;
      // Represents a Datetime value.
      Datetime datetime_value = 16;
      // Represents a Datetime value.
      google.protobuf.Timestamp datetimeoffset_value = 17;
      // Represents a decimal value.
      string decimal_value = 18;
    }
    int32 ManagedType = 19;
    bool SupportNull = 20;
  }
  // [END messages]
  ```

- Complex Primary Key schema:
  ```protobuf
  // [START declaration]
  syntax = "proto3";
  package storage;
  
  import "GenericValue.proto";
  // [END declaration]
  
  // [START java_declaration]
  option java_multiple_files = true;
  option java_package = "mases.entityframeworkcore.knet.serialization.protobuf";
  option java_outer_classname = "KeyContainer";
  // [END java_declaration]
  
  // [START csharp_declaration]
  option csharp_namespace = "MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage";
  // [END csharp_declaration]
  
  // [START messages]
  message PrimaryKeyType {
    repeated GenericValue values = 1; 
  }
  
  // Our address book file is just one of these.
  message KeyContainer {
    PrimaryKeyType PrimaryKey = 1;
  }
  // [END messages]
  ```


- ValueContainer schema:
  ```protobuf
  // [START declaration]
  syntax = "proto3";
  package storage;
  
  import "GenericValue.proto";
  // [END declaration]
  
  // [START java_declaration]
  option java_multiple_files = true;
  option java_package = "mases.entityframeworkcore.knet.serialization.protobuf";
  option java_outer_classname = "ValueContainer";
  // [END java_declaration]
  
  // [START csharp_declaration]
  option csharp_namespace = "MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage";
  // [END csharp_declaration]
  
  // [START messages]
  message PropertyDataRecord {
    int32 PropertyIndex = 1;
    string PropertyName = 2;
    string ClrType = 3;
    GenericValue Value = 4;
  }
  
  // Our address book file is just one of these.
  message ValueContainer {
    string EntityName = 1;
    string ClrType = 2;
    repeated PropertyDataRecord Data = 3;
  }
  // [END messages]
  ```

The extension converted this schema into code to speedup the exection of serialization/deserialization operations.

### How to use Protobuf 

`KafkaDbContext` contains three properties can be used to override the default types:
- **KeySerializationType**: set this value to `ProtobufKEFCoreSerDes.Key.BinaryRaw<>` or use `ProtobufKEFCoreSerDes.DefaultKeySerialization`, the type automatically manages simple or complex Primary Key
- **ValueSerializationType**: set this value to `ProtobufKEFCoreSerDes.ValueContainer.BinaryRaw<>` or use `ProtobufKEFCoreSerDes.DefaultValueContainerSerialization`
- **ValueContainerType**: set this value to `ProtobufValueContainerRaw<>` or use `ProtobufKEFCoreSerDes.DefaultValueContainer`

An example is:

```C#
using (context = new BloggingContext()
{
    BootstrapServers = "KAFKA-SERVER:9092",
    ApplicationId = "MyAppid",
    DbName = "MyDBName",
    KeySerializationType = typeof(ProtobufKEFCoreSerDes.Key<>),
    ValueContainerType = typeof(ProtobufValueContainer<>),
    ValueSerializationType = typeof(ProtobufKEFCoreSerDes.ValueContainer<>),
})
{
	// execute stuff here
}
```

## Complex type serialization

Some types can be marked as [ComplexType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.complextypeattribute) and EF Core will manage them differently from other types associated to the model.
An instance of EF Core using classic database provider will create, depends on the implementation, some extra columns to manage the content of each property of the CLR type marked as [ComplexType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.complextypeattribute).
KEFCore works in a different way since the content stored in Kafka contains the serializable information of the Entity plus the references to other types in the model.
KEFCore is based on the serializer described above and the new behavior supports conversion of ComplexType using two distinct behavior:
1. Enabling the default serialization based on Json format the complex type are threated as POCO object and the sub-system leave the responsability to the .NET Json serializer
2. Each type declared as [ComplexType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.complextypeattribute) can have its own converter which manages the specific object using specialized classes implementing [`IComplexTypeConverter` interface](https://github.com/masesgroup/KEFCore/blob/master/src/net/KEFCore.SerDes/IComplexTypeConverter.cs)

The [`IComplexTypeConverter` interface](https://github.com/masesgroup/KEFCore/blob/master/src/net/KEFCore.SerDes/IComplexTypeConverter.cs) is very simple:
```C#
public enum PreferredConversionType
{
    /// <summary>
    /// The preferred conversion expect a <see cref="string"/>, generally requested from text based serializers like Json
    /// </summary>
    Text,
    /// <summary>
    /// The preferred conversion expect a <see cref="byte"/> array, generally requested from binary based serializers like AVRO/Protobuf
    /// </summary>
    Binary
}

/// <summary>
/// The interface adds support to <see cref="IComplexTypeConverter"/> for logging behavior
/// </summary>
public interface IComplexTypeConverterLogging
{
    IDiagnosticsLogger<DbLoggerCategory.Infrastructure> Logging { get; }
    void Register(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logging);
}

/// <summary>
/// The interface shall be implemented and used from any external converter which neeeds to interact with serialization sub-system to manage <see cref="IComplexProperty"/>
/// </summary>
/// <remarks>The implementation of <see cref="Convert(PreferredConversionType, ref object?)"/> and <see cref="ConvertBack(PreferredConversionType, ref object?)"/> shall be thread-safe and the class shall have at least a default initializer</remarks>
public interface IComplexTypeConverter : IComplexTypeConverterLogging
{
    /// <summary>
    /// The set of <see cref="Type"/> supported from the converter
    /// </summary>
    IEnumerable<Type> SupportedClrTypes { get; }
    bool Convert(PreferredConversionType conversionType, ref object? data);
    bool ConvertBack(PreferredConversionType conversionType, ref object? data);
}

```

A single instance can support multiple `Type`s (this is the type declared as [ComplexType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.complextypeattribute)) and exposes two methods:
- `Convert`: converts EF Core types declared as [ComplexType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.complextypeattribute) to something manageable from the underlying serializer, so the final output can be one of the type declared in [Default managed types](#default-managed-types)
- `ConvertBack`: converts back something manageable from the underlying serializer (i.e. one of the type declared in [Default managed types](#default-managed-types)) to an EF Core types declared as [ComplexType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.complextypeattribute)

> [!TIP]
> For implementers: the parameter is passed with `ref` keyword to avoid an excessive memory pressure and the converted value shall replace the received input.

> [!IMPORTANT]
> The implementation of [`IComplexTypeConverter` interface](https://github.com/masesgroup/KEFCore/blob/master/src/net/KEFCore.SerDes/IComplexTypeConverter.cs) shall be thread-safe.

An user can build an external library and can register the custom converters within the system using the singleton service implementing [`IComplexTypeConverterFactory` interface](https://github.com/masesgroup/KEFCore/blob/master/src/net/KEFCore.SerDes/IComplexTypeConverterFactory.cs):
- using `GetService`:

  ```C#
  var context = new KakfaDbContext();
  context.GetService<IComplexTypeConverterFactory>();
  factory.Register(converter);
  ```
- using the methods available in [`KakfaDbContext`](kafkadbcontext.md)

### How to declare a complex property

Some good examples comes from https://www.learnentityframeworkcore.com/model/complex-type. 
An user can choose two different approaches:
- if the user is declaring its own model the [ComplexType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.complextypeattribute) is a good choice:

  ```C#
  [Table("TaxInfo", Schema = "ComplexTest")]
  public class TaxInfo
  {
      public int TaxInfoId { get; set; }
      public char Code { get; set; }
      public decimal Percentage { get; set; }
      [Required]
      public TaxInfoExtended TaxInfoExtended { get; set; }
      public int ExtraValue { get; set; } // used to check index consistency since the method GetIndex of IComplexProperty is zero-based 
  }

  [ComplexType]
  public class TaxInfoExtended
  {
      public int Code { get; set; }
      public decimal Percentage { get; set; }
  }
  ```
- however many time this kind of classes are defined in assemblies outside the control of the user (as requested from https://github.com/masesgroup/KEFCore/issues/445 the OPC-UA types are defined in specific assemblies) and EF Core offers the way to declare them programmatically:
  ```C#
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
      modelBuilder.Entity<TaxInfo>(x => { x.ComplexProperty(y => y.TaxInfoExtended, y => { y.IsRequired(); }); });
  	  base.OnModelCreating(modelBuilder);
  }
  ```

If the user is using the Json serialization there is no problem with the type, but with AVRO and Protobuf the type cannot be managed. A serializer shall be defined and registered:
```C#
public class TaxInfoExtendedConverter : IComplexTypeConverter
{
    public IEnumerable<Type> SupportedClrTypes => [typeof(TaxInfoExtended)];

    public bool Convert(PreferredConversionType conversionType, ref object input)
    {
        if (input is TaxInfoExtended taxInfoExtended)
        {
            input = $"{taxInfoExtended.CodeExtended}_{taxInfoExtended.PercentageExtended}";
            return true;
        }
        return false;
    }

    public bool ConvertBack(PreferredConversionType conversionType, ref object input)
    {
        if (input is string str)
        {
            try
            {
                var values = str.Split("_");
                var tie = new TaxInfoExtended
                {
                    CodeExtended = int.Parse(values[0]),
                    PercentageExtended = decimal.Parse(values[1])
                };
                input = tie;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TaxInfoExtendedConverter.ConvertBack failed for input '{str}': {ex}");
                input = new TaxInfoExtended();
                return true;
            }
        }
        return false;
    }
}
```

The previous in only an example which converts, and converts back, `TaxInfoExtended` using a string which can be managed from underlying sub-system.
