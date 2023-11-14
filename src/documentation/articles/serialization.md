# KEFCore: serialization

[Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) shall convert the entities used within the model in something viable from the backend.
Each backend has its own schema to convert entities into something else; database providers converts entities into database schema or blob in Cosmos.

> IMPORTANT NOTE: till the first major version, all releases shall be considered not stable: this means the API public, or internal, can change without notice.

## Basic concepts

[Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) shall convert the entities into record will be stored in the topics of Apache Kafka cluster.
The way the entities are converted shall follows a schema.
The current schema follows a JSON pattern and reports the information of each entity as:
- Primary Key:
  - Simple: if the Primary Key is a native type (e.g. int, long, double, and so on) the serialization is made using the Apache Kafka default serializer for that type
  - Complex: if the Primary Key is a complex type (e.g. int-int, int-long, int-string, and so on), Entity Framework reports it as an array of objects and the serialization is made using a JSON serializer

- Entity data: the Entity is managed, from [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/), as an array of objects associated to properties of the Entity. 
  The schema of the Apache Kafka record value follows the code definition in `DefaultValueContainer<T>`. Below two examples:
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

## Code and user override

The code is based on three elements shall be available to [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) in order to work:
- **ValueContainer type**: a type which encapsulate the Entity and stores needed information
- **Key SerDes**: the serializer of the Primary Key
- **ValueContainer SerDes**: the serializer of the ValueContainer


### Default types

[Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) comes with some default values:
- **ValueContainer** class: KEFCore uses `DefaultValueContainer<T>` (i.e. `DefaultKEFCoreSerDes.DefaultValueContainer`) which stores the CLR type of Entity, the properties ordered by their index with associated CLT type, name and JSON serializaed value; the class is marked for JSON serialization and it is used from the **ValueContainer SerDes**;
- **Key SerDes** class: KEFCore uses `DefaultKEFCoreSerDes.Key.Json<T>` (i.e. `DefaultKEFCoreSerDes.DefaultKeySerialization`), the type automatically manages simple or complex Primary Key
- **ValueContainer SerDes** class: KEFCore uses `DefaultKEFCoreSerDes.ValueContainer.Json<>` (i.e. `DefaultKEFCoreSerDes.DefaultValueContainerSerialization`)

### User override

The default serialization can be overridden with user defined **ValueContainer** class, **Key SerDes** or **ValueContainer SerDes**.

#### **ValueContainer** class

A custom **ValueContainer** class must contains enough information and shall follow the following rules:
- must implements the `IValueContainer<T>` interface
- must be a generic type
- must have at least a default constructor and a constructor which accept two parameters: a first parameter which is `IEntityType` and a second paramater of `object[]`

An example snippet is the follow:

```C#
public class CustomValueContainer<TKey> : IValueContainer<TKey> where TKey : notnull
{
    /// <summary>
    /// Initialize a new instance of <see cref="CustomValueContainer{TKey}"/>
    /// </summary>
    /// <param name="tName">The <see cref="IEntityType"/> requesting the ValueContainer for <paramref name="rData"/></param>
    /// <param name="rData">The data, built from EFCore, to be stored in the ValueContainer</param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a ValueContainer</remarks>
    public CustomValueContainer(IEntityType tName, object[] rData)
    {

    }

    /// <inheritdoc/>
    public string EntityName { get; set; }
    /// <inheritdoc/>
    public string ClrType { get; set; }
    /// <inheritdoc/>
    public void GetData(IEntityType tName, ref object[] array)
    {

    }
    /// <inheritdoc/>
    public IReadOnlyDictionary<int, string> GetProperties()
    {
        // build properties
    }
}
```

#### **Key SerDes** and **ValueContainer SerDes** class

A custom **Key SerDes** class shall follow the following rules:
- must implements the `IKNetSerDes<T>` interface or extend `KNetSerDes<T>`
- must be a generic type
- must have a parameterless constructor
- can store serialization information using Headers of Apache Kafka record (this information will be used from `EntityExtractor`)

An example snippet is the follow based on JSON serializer:

```C#
public class CustomKeySerDes<T> : KNetSerDes<T>
{
    readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(T).FullName!);
    readonly byte[] customSerDesName = Encoding.UTF8.GetBytes(typeof(CustomKeySerDes<>).FullName!);

    /// <inheritdoc cref="KNetSerDes{T}.Serialize(string, T)"/>
    public override byte[] Serialize(string topic, T data)
    {
        return SerializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="KNetSerDes{T}.SerializeWithHeaders(string, Headers, T)"/>
    public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
    {
        headers?.Add(KEFCoreSerDesNames.KeyTypeIdentifier, keyTypeName);
        headers?.Add(KEFCoreSerDesNames.KeySerializerIdentifier, customSerDesName);

        var jsonStr = System.Text.Json.JsonSerializer.Serialize<T>(data);
        return Encoding.UTF8.GetBytes(jsonStr);
    }
    /// <inheritdoc cref="KNetSerDes{T}.Deserialize(string, byte[])"/>
    public override T Deserialize(string topic, byte[] data)
    {
        return DeserializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="KNetSerDes{T}.DeserializeWithHeaders(string, Headers, byte[])"/>
    public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
    {
        if (data == null) return default;
        return System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
    }
}
```

```C#
public class CustomValueContainerSerDes<T> : KNetSerDes<T>
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

    /// <inheritdoc cref="KNetSerDes{T}.Serialize(string, T)"/>
    public override byte[] Serialize(string topic, T data)
    {
        return SerializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="KNetSerDes{T}.SerializeWithHeaders(string, Headers, T)"/>
    public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
    {
        headers?.Add(KEFCoreSerDesNames.ValueContainerSerializerIdentifier, valueContainerSerDesName);
        headers?.Add(KEFCoreSerDesNames.ValueContainerIdentifier, valueContainerName);

        var jsonStr = System.Text.Json.JsonSerializer.Serialize<T>(data);
        return Encoding.UTF8.GetBytes(jsonStr);
    }
    /// <inheritdoc cref="KNetSerDes{T}.Deserialize(string, byte[])"/>
    public override T Deserialize(string topic, byte[] data)
    {
        return DeserializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="KNetSerDes{T}.DeserializeWithHeaders(string, Headers, byte[])"/>
    public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
    {
        if (data == null) return default;
        return System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
    }
}
```

### How to use custom types 

`KafkaDbContext` contains three properties can be used to override the default types:
- **KeySerializationType**: set the value of the **Key SerDes** type in the form `CustomSerDes<>`
- **ValueSerializationType**: set the value of the **ValueContainer SerDes** type in the form `CustomSerDes<>`
- **ValueContainerType**: set the value of the **ValueContainer** type in the form `CustomValueContainer<>`

> **IMPORTANT NOTE**: the type applied in the previous properties of `KafkaDbContext` shall be a generic type definition, [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) will apply the right generic type when needed.

## **Avro** serialization

With package [MASES.EntityFrameworkCore.KNet.Serialization.Avro](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Avro/) an user can choose two different Avro serializers:
The engine comes with two different encoders 
- Binary
- Json

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
  	"doc": "Represents the storage container type to be used from KEFCore",
  	"fields": [
  		{
  			"name": "EntityName",
  			"type": "string"
  		},
  		{
  			"name": "ClrType",
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
  							"type": "int"
  						},
  						{
  							"name": "PropertyName",
  							"type": "string"
  						},
  						{
  							"name": "ClrType",
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
- **KeySerializationType**: set this value to `AvroKEFCoreSerDes.Key.Binary<>` or `AvroKEFCoreSerDes.Key.Json<>` or use `AvroKEFCoreSerDes.DefaultKeySerialization` (defaults to `AvroKEFCoreSerDes.Key.Binary<>`), both types automatically manages simple or complex Primary Key
- **ValueSerializationType**: set this value to `AvroKEFCoreSerDes.ValueContainer.Binary<>` or `AvroKEFCoreSerDes.ValueContainer.Json<>` or use `AvroKEFCoreSerDes.DefaultValueContainerSerialization` (defaults to `AvroKEFCoreSerDes.ValueContainer.Binary<>`)
- **ValueContainerType**: set this value to `AvroValueContainer<>` or use `AvroKEFCoreSerDes.DefaultValueContainer`

An example is:

```C#
using (context = new BloggingContext()
{
    BootstrapServers = "KAFKA-SERVER:9092",
    ApplicationId = "MyAppid",
    DbName = "MyDBName",
    KeySerializationType = UseAvroBinary ? typeof(AvroKEFCoreSerDes.Key.Binary<>) : typeof(AvroKEFCoreSerDes.Key.Json<>),
    ValueContainerType = typeof(AvroValueContainer<>),
    ValueSerializationType = UseAvroBinary ? typeof(AvroKEFCoreSerDes.ValueContainer.Binary<>) : typeof(AvroKEFCoreSerDes.ValueContainer.Json<>),
})
{
	// execute stuff here
}
```

## **Protobuf** serialization

With package [MASES.EntityFrameworkCore.KNet.Serialization.Protobuf](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf/) an user can choose the Protobuf serializer.

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
  // Our address book file is just one of these.
  message GenericValue {
    // The kind of value.
    oneof kind {
      // Represents a null value.
      google.protobuf.NullValue null_value = 1;
      // Represents a boolean value.
      bool bool_value = 2;
      // Represents a int value.
      int32 byte_value = 3;
      // Represents a int value.
      int32 short_value = 4;
      // Represents a int value.
      int32 int_value = 5;
      // Represents a long value.
      int64 long_value = 6;
      // Represents a float value.
      float float_value = 7;
      // Represents a double value.
      double double_value = 8;
      // Represents a string value.
      string string_value = 9;
      // Represents a Guid value.
      bytes guid_value = 10;
      // Represents a Timestamp value.
      google.protobuf.Timestamp datetime_value = 11;
      // Represents a Timestamp value.
      google.protobuf.Timestamp datetimeoffset_value = 12;
    }
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
- **KeySerializationType**: set this value to `ProtobufKEFCoreSerDes..Key.Binary<>` or use `ProtobufKEFCoreSerDes.DefaultKeySerialization`, the type automatically manages simple or complex Primary Key
- **ValueSerializationType**: set this value to `ProtobufKEFCoreSerDes.ValueContainer.Binary<>` or use `ProtobufKEFCoreSerDes.DefaultValueContainerSerialization`
- **ValueContainerType**: set this value to `ProtobufValueContainer<>` or use `ProtobufKEFCoreSerDes.DefaultValueContainer`

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