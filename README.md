# AgeDigitalTwins

AgeDigitalTwins is an SDK and API designed to support Digital Twins applications running on PostgreSQL with the Apache AGE extension. It supports DTDL (Digital Twins Definition Language) data models and allows for the validation of instances loaded in the graph database. The API and SDK are designed to make the transition from Azure Digital Twins easy.

## Features

- **DTDL Support**: Full support for DTDL data models.
- **Graph Database**: Utilizes PostgreSQL with the Apache AGE extension for graph database capabilities.
- **Validation**: Validate instances loaded in the graph database.
- **Easy Transition**: Designed to make the transition from Azure Digital Twins seamless.

## Roadmap

- **SDK**:
  - [x] Digital Twins CRUD operations
  - [x] Models CRUD operations
  - [x] Relationships CRUD operations
  - [x] Model Validation with DTDLParser
  - [x] Twin Validation with DTDLParser
  - [x] ADT Query Conversion (to Cypher)
    - [x] WHERE
    - [x] MATCH
    - [x] JOIN
    - [ ] Custom functions
  - [x] Error handling
  - [x] ETags
  - [ ] Pagination
  - [ ] Components
  - [ ] Telemetry
  - [ ] RBAC with user impersonation (same principle as in [Postgrest](https://docs.postgrest.org/en/v12/references/auth.html))
- **API**:
  - [x] Digital Twins CRUD operations
  - [x] Models CRUD operations
  - [x] Relationships CRUD operations
  - [x] Error Handling
  - [x] ETags
  - [ ] Authentication
- **Deployment**:
  - [x] Dockerize the API
  - [ ] Helm chart for deployment
- **Event routing**:
  - [x] Logical replication connection with AgType parser
  - [x] Data History output (same format as Azure Digital Twins)
  - [x] Event Notification output
  - [x] Kafka sink
  - [ ] Kusto sink (Azure Data Explorer / Fabric Real-Time)
  - [x] MQTT sink

## Contributing

Contributions are welcome! Please read our [contributing guidelines](CONTRIBUTING.md) for more information.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgements

- [Apache AGE](https://age.apache.org/)
- [Apache AGE C# client](https://github.com/Allison-E/pg-age)
- [Azure Digital Twins](https://azure.microsoft.com/en-us/services/digital-twins/)
