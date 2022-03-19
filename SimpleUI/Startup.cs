using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace SimpleUI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "Cookies";
                    options.DefaultChallengeScheme = "oidc";
                })
                .AddCookie("Cookies")
                .AddOpenIdConnect("oidc", options =>
                {
                    options.SignInScheme = "Cookies";
                    options.Authority = "https://demo.identityserver.io";

                    options.ClientId = "interactive.confidential";
                    options.ClientSecret = "secret";
                   
                    options.ResponseType = "code";
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.Scope.Add("api");
                    options.Scope.Add("offline_access");
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.SaveTokens = true;                    
                    options.Events.OnTicketReceived = e =>
                    {
                        e.Principal = TransformClaims(e.Principal);
                        return Task.CompletedTask;
                    };

                    options.Events.OnRedirectToIdentityProvider = context =>
                    {
                        // only modify requests to the authorization endpoint
                        if (context.ProtocolMessage.RequestType == OpenIdConnectRequestType.Authentication)
                        {
                            // generate code_verifier
                            var codeVerifier = CryptoRandom.CreateUniqueId(32);

                            // store codeVerifier for later use
                            context.Properties.Items.Add("code_verifier", codeVerifier);

                            // create code_challenge
                            string codeChallenge;
                            using (var sha256 = SHA256.Create())
                            {
                                var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                                codeChallenge = Base64Url.Encode(challengeBytes);
                            }

                            // add code_challenge and code_challenge_method to request
                            context.ProtocolMessage.Parameters.Add("code_challenge", codeChallenge);
                            context.ProtocolMessage.Parameters.Add("code_challenge_method", "S256");
                        }

                        return Task.CompletedTask;
                    };

                    options.Events.OnAuthorizationCodeReceived = context =>
                    {
                        // only when authorization code is being swapped for tokens
                        if (context.TokenEndpointRequest?.GrantType == OpenIdConnectGrantTypes.AuthorizationCode)
                        {
                            // get stored code_verifier
                            if (context.Properties.Items.TryGetValue("code_verifier", out var codeVerifier))
                            {
                                // add code_verifier to token request
                                context.TokenEndpointRequest.Parameters.Add("code_verifier", codeVerifier);
                            }
                        }

                        return Task.CompletedTask;
                    };
                });
            
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        private ClaimsPrincipal TransformClaims(ClaimsPrincipal principal)
        {            
            var claims = new List<Claim>();
            claims.AddRange(principal.Claims);  // retain any claims from originally authenticated user
            claims.Add(new Claim("junk", "garbage"));

            var newIdentity = new ClaimsIdentity(claims, principal.Identity.AuthenticationType, "name", "role");
            return new ClaimsPrincipal(newIdentity);
        }
        
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            //app.UseExceptionHandler("/Home/Error");
            app.UseHsts();            
            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseMiddleware<CustomErrorMiddleware>();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
