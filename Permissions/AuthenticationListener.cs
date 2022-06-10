using GraphQL.Server.Transports.Subscriptions.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using SER.Graphql.Reflection.NetCore.Builder;
using SER.Graphql.Reflection.NetCore.Utilities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Permissions
{
    public class AuthenticationListener : IOperationMessageListener
    {
        public static readonly string PRINCIPAL_KEY = "User";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _config;
        private readonly IOptionsMonitor<SERGraphQlOptions> _optionsDelegate;


        public AuthenticationListener(IHttpContextAccessor contextAccessor, IConfiguration config, IOptionsMonitor<SERGraphQlOptions> optionsDelegate)
        {
            _httpContextAccessor = contextAccessor;
            _config = config;
            _optionsDelegate = optionsDelegate;
        }

        private ClaimsPrincipal BuildClaimsPrincipal(string bearer)
        {
            if (string.IsNullOrEmpty(_optionsDelegate.CurrentValue.SecurityKey) ||
                string.IsNullOrEmpty(_optionsDelegate.CurrentValue.SigningKey) ||
                string.IsNullOrEmpty(_optionsDelegate.CurrentValue.TokenIssuer)
                ) return null;
            // Your code here
            // A user context builder can be included via constructor injection,
            //  and possibly the Authorization payload can be set (hacked)
            //  to the Authorization header on the http context
            var encryptationKey = new SymmetricSecurityKey(_optionsDelegate.CurrentValue.SecurityKey.CreateKey());
            var signinKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_optionsDelegate.CurrentValue.SigningKey));

            var tokenParams = new TokenValidationParameters
            {
                ValidIssuer = new Uri(_optionsDelegate.CurrentValue.TokenIssuer).ToString(),
                ValidateAudience = false,
                IssuerSigningKey = signinKey,
                NameClaimType = "name",
                RoleClaimType = "role",
                TokenDecryptionKey = encryptationKey
            };
            try
            {
                JwtSecurityTokenHandler handler = new();
                return handler.ValidateToken(bearer, tokenParams, out SecurityToken token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }


        public Task BeforeHandleAsync(MessageHandlingContext context)
        {
            if (context.Message.Type == MessageType.GQL_CONNECTION_INIT)
            {
                var payload = context.Message.Payload as JObject;
                if (payload != null && payload.TryGetValue("Authorization", StringComparison.CurrentCulture, out JToken token))
                {
                    var auth = payload.Value<string>("Authorization");
                    // Save the user to the http context
                    _httpContextAccessor.HttpContext.User = BuildClaimsPrincipal(auth.Split("Bearer ")[1]);
                }
            }

            // Always insert the http context user into the message handling context properties
            // Note: any IDisposable item inside the properties bag will be disposed after this message is handled!
            //  So do not insert such items here, but use something like 'context[PRINCIPAL_KEY] = [...]'
            context.Properties[PRINCIPAL_KEY] = _httpContextAccessor.HttpContext.User;
            //context[PRINCIPAL_KEY] = _httpContextAccessor.HttpContext.User;

            return Task.CompletedTask;
        }

        public Task HandleAsync(MessageHandlingContext context) => Task.CompletedTask;
        public Task AfterHandleAsync(MessageHandlingContext context) => Task.CompletedTask;
    }
}
