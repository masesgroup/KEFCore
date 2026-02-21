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
- must have at least a default constructor and a constructor which accept three parameters in this order:
  1. `IEntityType` 
  2. `IProperty[]?`
  3. `object[]`

An example snippet is the follow:

```C#
public class CustomValueContainer<TKey> : IValueContainer<TKey> where TKey : notnull
{
    /// <summary>
    /// Initialize a new instance of <see cref="CustomValueContainer{TKey}"/>
    /// </summary>
    /// <param name="tName">The <see cref="IEntityType"/> requesting the ValueContainer for <paramref name="rData"/></param>
    /// <param name="properties">The set of <see cref="IProperty"/> deducted from <see cref="IEntityType.GetProperties"/>, if <see langword="null"/> the implmenting instance of <see cref="IValueContainer{T}"/> shall deduct it</param>
    /// <param name="rData">The data, built from EFCore, to be stored in the ValueContainer</param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a ValueContainer</remarks>
    public CustomValueContainer(IEntityType tName, IProperty[]? properties, object[] rData)
    {
        properties ??= [.. tName.GetProperties()];

    }

    /// <summary>
    /// The Entity name of <see cref="IEntityType"/>
    /// </summary>
    public string EntityName { get; set; }
    /// <summary>
    /// The CLR <see cref="Type"/> of <see cref="IEntityType"/>
    /// </summary>
    string ClrType { get; set; }
    /// <summary>
    /// Returns back the raw data associated to the Entity contained in <see cref="IValueContainer{T}"/> instance
    /// </summary>
    /// <param name="tName">The requesting <see cref="IEntityType"/> to get the data back, can <see langword="null"/> if not available</param>
    /// <param name="properties">The set of <see cref="IProperty"/> deducted from <see cref="IEntityType.GetProperties"/>, if <see langword="null"/> the implmenting instance of <see cref="IValueContainer{T}"/> shall deduct it</param>
    /// <param name="array">The array of object to be filled in with the data stored in the ValueContainer</param>
    void GetData(IEntityType tName, IProperty[]? properties, ref object[] array)
    {
        // add specific logic
    }
    /// <summary>
    /// Returns back a dictionary of properties (PropertyName, Value) associated to the Entity
    /// </summary>
    /// <returns>A dictionary of properties (PropertyName, Value) filled in with the data stored in the ValueContainer</returns>
    public IDictionary<string, object?> GetProperties()
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