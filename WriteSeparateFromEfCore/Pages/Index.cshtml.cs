using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serilog;
using WriteSeparateFromEfCore.Classes;
using WriteSeparateFromEfCore.Data;

namespace WriteSeparateFromEfCore.Pages;
public class IndexModel : PageModel
{

    private readonly Context _context;

    public IndexModel(Context context)
    {
        _context = context;
        CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(1));

        var success = context.CanConnectAsync(cancellationTokenSource.Token);

        if (success == false)
        {
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
        }

    }
    public void OnGet()
    {

    }

    public void OnPostInvokeException(int id)
    {
        DoNotUseThisInYourCode(id);
        //UseThisForReadApp(id);
    }

    private void UseThisForReadApp(int id)
    {
        var user = _context.UserLogin.FirstOrDefault(x => x.Id == id);
        if (user == null)
        {
            Log.Information("No user with an id of {P1}", id);
        }
        else
        {
            Log.Information("Found user with email address {P1}", user.EmailAddress);
        }
    }

    private void DoNotUseThisInYourCode(int id)
    {
        Log.Information("OnGet - before throwing exception with {P1}", id);
        try
        {
            // there is no record with this id
            var user = _context.UserLogin.First(x => x.Id == id);
        }
        catch (Exception e)
        {
            Log.Error(e, "");
        }

        Log.Information("OnGet - after throwing exception");
    }
}
