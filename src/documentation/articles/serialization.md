# KEFCore: serialization

[Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) shall convert the entities used within the model in something viable from the backend.
Each backend has its own schema to convert entities into something else; database providers converts entities into database schema or blob in Cosmos.

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
- **ValueContainer** class: KEFCore uses `DefaultValueContainer<T>` which stores the CLR type of Entity, the properties ordered by their index with associated CLT type, name and JSON serializaed value; the class is marked for JSON serialization and it is used from the **ValueContainer SerDes**;
- **Key SerDes** class: KEFCore uses `KNetSerDes<T>` for simple Primary Key or `KEFCoreSerDes<T>` for complex Primary Key
- **ValueContainer SerDes** class: KEFCore uses `KEFCoreSerDes<T>`

### User override

The default serialization can be overridden with user defined **ValueContainer** class, **Key SerDes** or **ValueContainer SerDes**.

#### **ValueContainer** class

A custom **ValueContainer** class must contains enough information and shall follow the following rules:
- must implements the `IValueContainer<T>` interface
- must be a generic type
- must have at least a constructor which accept two parameters: a first parameter which is `IEntityType` and a second paramater of `object[]`

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

    public void GetData(IEntityType tName, ref object[] array)
    {

    }
}
```

#### **Key SerDes** and **ValueContainer SerDes** class

A custom **Key SerDes** class shall follow the following rules:
- must implements the `IKNetSerDes<T>` interface or extend `KNetSerDes<T>`
- must be a generic type
- must have a parameterless constructor

An example snippet is the follow based on JSON serializer:

```C#
public class CustomSerDes<T> : KNetSerDes<T>
{
    /// <inheritdoc cref="KNetSerDes{T}.Serialize(string, T)"/>
    public override byte[] Serialize(string topic, T data)
    {
        return SerializeWithHeaders(topic, null, data);
    }
    /// <inheritdoc cref="KNetSerDes{T}.SerializeWithHeaders(string, Headers, T)"/>
    public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
    {
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

### How to use custom serialzer 

`KafkaDbContext` contains three properties can be used to override the default types:
- **KeySerializationType**: set the value of the **Key SerDes** type in the form `CustomSerDes<>`
- **ValueSerializationType**: set the value of the **ValueContainer SerDes** type in the form `CustomSerDes<>`
- **ValueContainerType**: set the value of the **ValueContainer** type in the form `CustomValueContainer<>`

> **IMPORTANT NOTE**: the type applied in the previous properties of `KafkaDbContext` shall be a generic type definition, [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) will apply the right generic type when needed.

