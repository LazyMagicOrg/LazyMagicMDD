# AGENTS.md - ODataToOpenApi Converter

## Overview

This tool converts OData CSDL metadata (XML) to OpenAPI 3.1 specifications (YAML). The conversion uses the `Microsoft.OpenApi.OData` library as a base, then applies multiple post-processing transformations to produce clean, usable OpenAPI specs compatible with code generators like NSwag.

## Build & Run Commands

```bash
dotnet build ODataToOpenApi -c Release           # Build the converter
dotnet run --project ODataToOpenApi -c Release   # Run with default paths
dotnet run --project ODataToOpenApi -c Release -- <input.xml> <output.yaml>  # Custom paths
```

## Transformation Rules

The following rules are applied in sequence to transform OData metadata into clean OpenAPI specs:

### 1. Path Syntax Conversion

**Rule**: Convert OData function/action path syntax to REST-style paths.

- `GetDeliveryDate(Id={Id})` → `GetDeliveryDate/{Id}`
- `Func(A={A},B={B})` → `Func/{A}/{B}`
- `DoSomething()` → `DoSomething`

**Rationale**: REST-style paths are more standard and work better with routing frameworks.

### 2. Preserve Original OData Path

**Rule**: Add `x-lz-odatapath` extension to ALL operations containing the original OData path.

```yaml
x-lz-odatapath: '/odata/v1/DeliveryTimes/GetDeliveryDate(Id={Id})'
```

**Rationale**: Downstream tools may need the original OData path for flow-through operations or OData client generation.

### 3. Simplify Component Names

**Rule**: Remove namespace prefixes from schema names where possible.

- `Smartstore.Core.Common.Address` → `Address`
- `Default.ODataErrors.ODataError` → `ODataError`
- When conflicts exist, use camelCase disambiguation: `CatalogProductsProduct`

**Rationale**: Shorter names produce cleaner generated code and are easier to work with.

### 4. Extract Inline Schemas to Components

**Rule**: Extract all inline `type: object` schemas from paths AND `components/responses` to `components/schemas`.

**Before**:
```yaml
requestBody:
  content:
    application/json:
      schema:
        type: object
        properties:
          discountIds:
            type: array
```

**After**:
```yaml
requestBody:
  content:
    application/json:
      schema:
        $ref: '#/components/schemas/CategoriesCategoryApplyDiscountsRequest'
```

**Rationale**: 
- Prevents NSwag from generating anonymous types like `Response`, `Response2`, etc.
- Enables schema reuse across operations
- Produces cleaner, more maintainable generated code

### 5. Reuse Existing Schemas

**Rule**: When extracting inline schemas, match against existing components by content hash and reuse them.

**Rationale**: Reduces schema duplication and ensures consistent types across the API.

### 6. Remove Examples Section

**Rule**: Remove the `components/examples` section entirely.

**Rationale**: Examples can contain OData-specific syntax that causes parsing issues in downstream tools.

### 7. Convert OpenAPI 3.1 Type Arrays to Nullable Syntax

**Rule**: Convert type arrays to single type with `nullable: true`.

**Before** (OpenAPI 3.1 style):
```yaml
type:
  - 'null'
  - string
```

**After**:
```yaml
type: string
nullable: true
```

**Rationale**: Many code generators (including NSwag) expect `type` to be a string, not an array.

### 8. Convert anyOf Null Patterns

**Rule**: Convert `anyOf` with `type: 'null'` entry to `nullable: true`.

**Before**:
```yaml
anyOf:
  - $ref: '#/components/schemas/Country'
  - type: 'null'
```

**After**:
```yaml
anyOf:
  - $ref: '#/components/schemas/Country'
nullable: true
```

**Rationale**: Cleaner representation that code generators handle better.

### 9. Simplify oneOf Declarations

**Rule**: When `oneOf` contains multiple type options, pick one based on priority: `$ref` > `number` > `string`.

**Before**:
```yaml
oneOf:
  - type: number
    format: float
  - type: string
  - $ref: '#/components/schemas/ReferenceNumeric'
```

**After**:
```yaml
$ref: '#/components/schemas/ReferenceNumeric'
nullable: true
```

**Rationale**: OData uses `oneOf` for flexible typing (e.g., numbers that could be strings like "INF", "NaN"). For strongly-typed languages, picking one type is more practical.

### 10. Rename Conflicting Schema Names

**Rule**: Rename schemas that conflict with common .NET types.

| Original Name | Renamed To |
|--------------|------------|
| `StreamContent` | `ODataStreamContent` |
| `HttpContent` | `ODataHttpContent` |
| `ByteArrayContent` | `ODataByteArrayContent` |
| `StringContent` | `ODataStringContent` |
| `FormUrlEncodedContent` | `ODataFormUrlEncodedContent` |
| `MultipartContent` | `ODataMultipartContent` |
| `MultipartFormDataContent` | `ODataMultipartFormDataContent` |

**Rationale**: Prevents ambiguous reference errors when generated code imports both the schema namespace and `System.Net.Http`.

## Processing Order

The transformations are applied in this specific order:

1. Load OData CSDL metadata
2. Convert to OpenAPI using `Microsoft.OpenApi.OData`
3. Add `x-lz-odatapath` extensions to all operations
4. Convert function path syntax to REST-style
5. Serialize to YAML
6. Simplify component names (remove namespace prefixes)
7. Extract inline schemas to components (from paths AND responses)
8. Remove examples section
9. Convert type arrays to nullable syntax
10. Simplify oneOf declarations
11. Rename conflicting schema names
12. Write final YAML

## Key Settings for Microsoft.OpenApi.OData

```csharp
var settings = new OpenApiConvertSettings
{
    EnableKeyAsSegment = true,           // /entity/{Id} instead of /entity({Id})
    EnableOperationId = true,            // Generate operation IDs
    OpenApiSpecVersion = OpenApiSpecVersion.OpenApi3_1,
    PathPrefix = "odata/v1",             // Add path prefix
    EnableNavigationPropertyPath = true, // Include navigation paths
    EnableOperationPath = true,          // Include function/action paths
    EnableOperationImportPath = true,
    EnablePagination = false,
    EnableCount = false,
    EnableDollarCountPath = false,
    EnableUnqualifiedCall = true         // Remove Default. namespace prefix
};
```

## Common Issues and Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| `CS0104: 'StreamContent' is ambiguous` | Schema name conflicts with .NET type | Rule 10: Rename conflicting schemas |
| `CS0246: 'Response' could not be found` | Inline schema in response not extracted | Rule 4: Extract from `components/responses` |
| YAML parse error on `type` field | `type` is an array, not string | Rule 7: Convert type arrays |
| Stack overflow during processing | Circular references in OpenAPI object model | Use YAML string manipulation instead of object model |

## Dependencies

- `Microsoft.OpenApi` v2.0.0
- `Microsoft.OpenApi.OData` v2.0.0
- `YamlDotNet` v16.3.0
