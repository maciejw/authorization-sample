using Microsoft.AspNetCore.Authentication.JwtBearer;

public static class AuthenticationSchemes
{
    public const string Users = JwtBearerDefaults.AuthenticationScheme;
    public const string Services = "ServiceBearer";
}
