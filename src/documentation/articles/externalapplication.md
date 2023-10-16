# KEFCore: external application

[Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) shall convert the entities used within the model in something viable from the backend.
Continuing from the concepts introduced in [serialization](serialization.md), an external application can use the data stored in the topics in a way it decide: [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) gives some helpers to get back the CLR Entity objects stored in the topics.

> IMPORTANT NOTE: till the first major version, all releases shall be considered not stable: this means the API public, or internal, can be change without notice.

## Basic concepts

An external application may want to be informed about data changes in the topics and want to analyze the Entity was previously managed from the EFCore application.
Within the core packages there is the `EntityExtractor` class which contains, till now, a method accepting a raw `ConsumerRecord<byte[], byte[]>` from Apache Kafka.
The method reads the info stored in it and returns the Entity object with the filled properties.

It is possible to build a new application which subscribe to a topic created from the EFCore application.
The following is a possible snippet of the logic can be applied:

```c#
const string topicFrom = "TheKEFCoreTopicWithData";

KafkaConsumer<byte[], byte[]> consumer = new KafkaConsumer<byte[], byte[]>();
consumer.Subscribe(topicFrom); // the callback was omitted for simplicity

var records = consumer.Poll(100);

foreach(var record in records)
{
	var entity = EntityExtractor.FromRecord(record);
	Console.WriteLine(entity);
}
```

A full working example can be found under test folder of the [repository](https://github.com/masesgroup/KEFCore).

## Possible usages

TDB