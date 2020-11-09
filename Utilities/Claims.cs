using System;
using System.Collections.Generic;
using System.Text;

namespace Graphql.AutoReflection.NetCore.Utilities
{
    public static class Claims
    {
        public const string AccessTokenHash = "at_hash";
        public const string Nickname = "nickname";
        public const string Nonce = "nonce";
        public const string NotBefore = "nbf";
        public const string PhoneNumber = "phone_number";
        public const string PhoneNumberVerified = "phone_number_verified";
        public const string Picture = "picture";
        public const string PostalCode = "postal_code";
        public const string PreferredUsername = "preferred_username";
        public const string Profile = "profile";
        public const string Name = "name";
        public const string Region = "region";
        public const string Scope = "scope";
        public const string StreetAddress = "street_address";
        public const string Subject = "sub";
        public const string TokenType = "token_type";
        public const string TokenUsage = "token_usage";
        public const string UpdatedAt = "updated_at";
        public const string Username = "username";
        public const string Website = "website";
        public const string Zoneinfo = "zoneinfo";
        public const string Role = "role";
        public const string MiddleName = "middle_name";
        public const string KeyId = "kid";
        public const string Country = "country";
        public const string Audience = "aud";
        public const string AuthenticationContextReference = "acr";
        public const string AuthenticationMethodReference = "amr";
        public const string AuthenticationTime = "auth_time";
        public const string AuthorizedParty = "azp";
        public const string Birthdate = "birthdate";
        public const string ClientId = "client_id";
        public const string CodeHash = "c_hash";
        public const string JwtId = "jti";
        public const string Address = "address";
        public const string Email = "email";
        public const string ExpiresAt = "exp";
        public const string FamilyName = "family_name";
        public const string Formatted = "formatted";
        public const string Gender = "gender";
        public const string GivenName = "given_name";
        public const string IssuedAt = "iat";
        public const string Issuer = "iss";
        public const string Locale = "locale";
        public const string Locality = "locality";
        public const string EmailVerified = "email_verified";
        public const string Active = "active";

        public static class Prefixes
        {
            public const string Private = "oi_";
        }
        public static class Private
        {
            public const string AccessTokenLifetime = "oi_act_lft";
            public const string TokenId = "oi_tkn_id";
            public const string Scope = "oi_scp";
            public const string Resource = "oi_rsrc";
            public const string RefreshTokenLifetime = "oi_reft_lft";
            public const string RedirectUri = "oi_reduri";
            public const string Presenter = "oi_prst";
            public const string Nonce = "oi_nce";
            public const string IdentityTokenLifetime = "oi_idt_lft";
            public const string TokenType = "oi_tkn_typ";
            public const string ExpirationDate = "oi_exp_dt";
            public const string DeviceCodeId = "oi_dvc_id";
            public const string CreationDate = "oi_crt_dt";
            public const string CodeChallengeMethod = "oi_cd_chlg_meth";
            public const string CodeChallenge = "oi_cd_chlg";
            public const string ClaimDestinationsMap = "oi_cl_dstn";
            public const string AuthorizationId = "oi_au_id";
            public const string AuthorizationCodeLifetime = "oi_auc_lft";
            public const string Audience = "oi_aud";
            public const string DeviceCodeLifetime = "oi_dvc_lft";
            public const string UserCodeLifetime = "oi_usrc_lft";
        }
    }
}
