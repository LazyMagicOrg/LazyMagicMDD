namespace LazyMagicGenerator.Test;
using LazyMagic;

using Xunit;

public class GenerateOperationIdTests
{
    [Theory]
    [InlineData(null, "/path", "")]
    [InlineData("get", null, "")]
    [InlineData(null, null, "")]
    public void GenerateOperationId_WithNullInputs_ReturnsEmptyString(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/yada/bada/{id}", "GetYadaBadaId")]
    [InlineData("get", "yada/bada/{id}", "GetYadaBadaId")]
    [InlineData("post", "/yada/bada/{id}", "PostYadaBadaId")]
    [InlineData("put", "/yada/bada/{id}", "PutYadaBadaId")]
    [InlineData("delete", "/yada/bada/{id}", "DeleteYadaBadaId")]
    [InlineData("patch", "/yada/bada/{id}", "PatchYadaBadaId")]
    [InlineData("head", "/yada/bada/{id}", "HeadYadaBadaId")]
    [InlineData("options", "/yada/bada/{id}", "OptionsYadaBadaId")]
    public void GenerateOperationId_WithDifferentHttpOperations_ReturnsCorrectFormat(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("GET", "/users", "GetUsers")]
    [InlineData("POST", "/users", "PostUsers")]
    [InlineData("gEt", "/users", "GetUsers")]
    public void GenerateOperationId_WithMixedCaseOperations_CapitalizesFirstLetterOnly(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/", "Get")]
    [InlineData("get", "", "Get")]
    [InlineData("get", "///", "Get")]
    public void GenerateOperationId_WithEmptyOrSlashOnlyPaths_ReturnsOperationOnly(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/users", "GetUsers")]
    [InlineData("get", "users", "GetUsers")]
    [InlineData("get", "users/", "GetUsers")]
    [InlineData("get", "/users/", "GetUsers")]
    public void GenerateOperationId_WithSimplePaths_HandlesSlashesCorrectly(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/api/v1/users", "GetApiV1Users")]
    [InlineData("get", "/api/v2/products/categories", "GetApiV2ProductsCategories")]
    [InlineData("get", "/deeply/nested/resource/path", "GetDeeplyNestedResourcePath")]
    public void GenerateOperationId_WithMultiplePathSegments_ConcatenatesAllSegments(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/users/{id}", "GetUsersId")]
    [InlineData("get", "/users/{userId}", "GetUsersUserid")]
    [InlineData("get", "/users/{id}/posts/{postId}", "GetUsersIdPostsPostid")]
    [InlineData("get", "/{controller}/{action}/{id}", "GetControllerActionId")]
    public void GenerateOperationId_WithPathParameters_RemovesBracesAndFormatsCorrectly(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/USERS", "GetUsers")]
    [InlineData("get", "/UsErS", "GetUsers")]
    [InlineData("get", "/users/POSTS", "GetUsersPosts")]
    [InlineData("get", "/API/V1/USERS", "GetApiV1Users")]
    public void GenerateOperationId_WithMixedCasePaths_ConvertsToProperCase(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/users//posts", "GetUsersPosts")]
    [InlineData("get", "//users///posts//", "GetUsersPosts")]
    [InlineData("get", "/users/{}/posts", "GetUsersPosts")]
    public void GenerateOperationId_WithEmptySegments_IgnoresEmptyParts(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/users/{id}/orders/{orderId}/items/{itemId}", "GetUsersIdOrdersOrderidItemsItemid")]
    [InlineData("post", "/api/v2/customers/{customerId}/addresses/{addressId}", "PostApiV2CustomersCustomeridAddressesAddressid")]
    public void GenerateOperationId_WithComplexPaths_HandlesCorrectly(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateOperationId_WithSingleCharacterOperation_HandlesCorrectly()
    {
        // Arrange
        var op = "g";
        var path = "/users";
        var expected = "GUsers";

        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get", "/users-list", "GetUsers_list")]
    [InlineData("get", "/user_profile", "GetUser_profile")]
    [InlineData("get", "/user.details", "GetUser_details")]
    public void GenerateOperationId_WithSpecialCharactersInPath_PreservesCharacters(string op, string path, string expected)
    {
        // Act
        var result = OpenApiUtils.GenerateOperationId(op, path);

        // Assert
        Assert.Equal(expected, result);
    }
}

// Note: Replace 'OpenApiUtils' with the actual class name containing the GenerateOperationId method