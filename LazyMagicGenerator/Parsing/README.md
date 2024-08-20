# LazyMagic Class Summary

This document provides an overview of the key classes in the LazyMagic YAML deserialization and type conversion.

## ArtifactPropertyConverter

- Inherits from `YamlTypeConverterBase` and implements `IYamlTypeConverter`
- Handles deserialization of `ArtifactBase` objects
- Contains a dictionary of artifact types for dynamic type resolution
- Implements custom YAML reading logic to handle different artifact types

## ArtifactsPropertyConverter

- Inherits from `YamlTypeConverterBase` and implements `IYamlTypeConverter`
- Responsible for deserializing `Artifacts` collections
- Uses `ArtifactPropertyConverter` to deserialize individual artifacts

## DetailedErrorNodeDeserializer

- Implements `INodeDeserializer`
- Wraps another deserializer to provide more detailed error information
- Captures parsing events to construct property paths for error reporting
- Throws `DetailedYamlException` with enhanced error messages

## DirectivePropertyConverter

- Inherits from `YamlTypeConverterBase` and implements `IYamlTypeConverter`
- Handles deserialization of `DirectiveBase` objects
- Contains a dictionary of directive types for dynamic type resolution
- Implements custom YAML reading logic to handle different directive types and artifacts

## DirectivesPropertyConverter

- Inherits from `YamlTypeConverterBase` and implements `IYamlTypeConverter`
- Responsible for deserializing `Directives` collections
- Uses `DirectivePropertyConverter` to deserialize individual directives

## YamlTypeConverterBase

- Abstract base class for YAML type converters
- Provides common functionality for YAML deserialization
- Includes methods for consuming YAML nodes, sequences, and mappings
- Initializes serializer and deserializer with custom error handling

These classes work together to provide a robust YAML deserialization system for the LazyMagic project, with a focus on handling artifacts, directives, and providing detailed error information during the deserialization process.