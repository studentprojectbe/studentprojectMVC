using Matrixsoft.PwnedPasswords;
using Matrixsoft.PwnedPasswords.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using studentprojectMVC.Auth;
using studentprojectMVC.DbContexts;
using studentprojectMVC.Models;
using studentprojectMVC.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace studentprojectMVC
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https: /go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
                options.HttpsPort = 443;
            });
            //// GDPR - Consent ===  Temporarily disable cookie
            //services.Configure<CookiePolicyOptions>(options =>
            //{
            //    options.CheckConsentNeeded = context => true;
            //    options.MinimumSameSitePolicy = SameSiteMode.None;
            //});

            services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(7);
                options.Lockout.MaxFailedAccessAttempts = 3;
            });

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
                options.ExcludedHosts.Add("example.com");
                options.ExcludedHosts.Add("www.example.com");
            });
            // AntiForgery Expanded            
            //services.AddAntiforgery();
            //services.AddAntiforgery(options => { options.RequireSsl = true; });
            // Anti Forgery as global filter
            services.AddMvc(OptionsBuilderConfigurationExtensions => 
                OptionsBuilderConfigurationExtensions.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

            //var connection = Configuration.GetConnectionString("studentprojectMVCDb");  DefaultConnection
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            //services.AddDefaultIdentity<IdentityUser>(Options => Options.SignIn.RequireConfirmedAccount = true)
            //    .AddPwnedPasswordsValidator<IdentityUser>()
            //    .AddEntityFrameworkStores<AppDbContext>();

            //services.AddDefaultIdentity<IdentityUser>(); //.AddEntityFrameworkStores<AppDbContext>();
            //services.AddIdentity<IdentityUser OR ApplicationUser, IdentityRole>(Options =>
            services.AddIdentity<ApplicationUser, IdentityRole>(Options =>            
            {
                Options.Password.RequiredLength = 8;
                Options.Password.RequireNonAlphanumeric = true;
                Options.Password.RequireUppercase = true;
                Options.User.RequireUniqueEmail = true;
                Options.SignIn.RequireConfirmedAccount = true;
            })
                .AddPwnedPasswordsValidator<ApplicationUser>()   // <IdentityUser>
                .AddDefaultUI()
                .AddDefaultTokenProviders()
                .AddEntityFrameworkStores<AppDbContext>();

            //services.AddIdentity<IdentityUser, IdentityRole>()
            //    .AddEntityFrameworkStores<AppDbContext>();

            services.AddSession(options =>
            {
                options.Cookie.IsEssential = true;
            });

            //--// services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            //studentprojectMVCDb  --  DefaultConnection

            // IWebHostEnvironment (stored in _env) is injected into the Startup class.
            //if (! env.IsDevelopment())
            //{
            //    services.AddHttpsRedirection(options =>
            //    {
            //        options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
            //        options.HttpsPort = 443;
            //    });
            //}
            
            // register own services and repositories
            services.AddScoped<IRecordRepository, RecordRepository>();
            services.AddScoped<IGenreRepository, GenreRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<ShoppingCart>(sp => ShoppingCart.GetCart(sp));

            services.AddHttpContextAccessor();
            //services.AddSession();
            
            //// CORS configuration
            var allowedOrigins = Configuration.GetValue<string>("AllowedOrigins")?.Split(",") ?? new string[0];
            services.AddCors(options => options.AddPolicy("studentprojectMVCInternal", builder => builder.WithOrigins(allowedOrigins)));
            services.AddAntiforgery();

            services.AddTransient<PwnedPasswordsClient>();

            services.AddPwnedPasswordHttpClient(minimumFrequencyToConsiderPwned: 1)
                .AddTransientHttpErrorPolicy(p => p.RetryAsync(3))
                .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(2)));

            //// services.AddControllersWithViews();  
            services.AddAuthentication();
            // Claims-based Authorization - Custom policy restricting to CountryLocation to Belgium
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdministratorOnly", policy => policy.RequireRole("Administrators"));
                options.AddPolicy("OfficeSupport", policy => policy.RequireRole("Office Support"));
                options.AddPolicy("DeleteRecord", policy => policy.RequireClaim("Delete Record", "Delete Record"));
                options.AddPolicy("AddRecord", policy => policy.RequireClaim("Add Record", "Add Record"));
                options.AddPolicy("CountryLocation", policy => policy.Requirements.Add(new CountryLocationRequirement("Belgium")));
            });
            //services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme);
            //services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            //    .AddCookie(Options => Options.LoginPath = "/Identity/Account/Login");
            services.Configure<CookiePolicyOptions>(Options =>
            {
                Options.CheckConsentNeeded = context => true;
                Options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            /*services.AddControllersWithViews(Options =>
                Options.Filters.Add(new RequireHttpsAttribute())
                );*/
            // combine with --> services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddControllersWithViews(Options =>
                Options.Filters.Add(new RequireHttpsAttribute())
                ).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            //services.AddRazorPages();
            services.AddRazorPages().AddMvcOptions(Options => Options.Filters.Add(new AuthorizeFilter())); // [AllowAnonymous]
            //// begin add
            // https:/ docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-5.0
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });                      

            //services.AddHttpsRedirection(options =>
            //{
            //    options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
            //    options.HttpsPort = 443;
            //});

            //services.AddHttpsRedirection(options =>
            //{
            //    options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
            //    options.HttpsPort = 443;
            //});
            //// end add            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHsts();
            app.UseHttpsRedirection();
            app.UseStaticFiles(); // use www/root

            //app.UseIdentity(); -- obsolete, replaced by app.UseAuthentication
            //// redirect HTTP requests to HTTPS
            //??app.UseHttpsRedirection();
            //// begin add
            ////
            /*app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
                context.Response.Headers.Add("Referrer-Policy", "same-origin");
                //context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                await next();
            });*/
            //// end add
            //// add support for text-only headers for common status codes, such as status '400'
            
            app.UseCookiePolicy(); // ===  Temporarily disable cookie
                        
            app.UseRouting();
            app.UseCors("studentprojectMVCInternal");

            app.UseAuthentication(); // replaces app.UseIdentity();
            app.UseAuthorization();
            app.UseSession();
            
            app.UseStatusCodePages();
            //// begin add
            app.UseSecurityHeaders();

            ////app.UseReferrerPolicy(p => p.SameOrigin());
            
            ////app.UseCookiePolicy(new CookiePolicyOptions
            ////{
            ////    HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always,
            ////    MinimumSameSitePolicy = SameSiteMode.Strict,
            ////    Secure = CookieSecurePolicy.Always
            ////});
            /// end add         
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");                
            });
        }
    }
}
