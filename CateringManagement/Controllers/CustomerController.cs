using CateringManagement.CustomControllers;
using CateringManagement.Data;
using CateringManagement.Models;
using CateringManagement.Utilities;
using CateringManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Numerics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using String = System.String;

namespace CateringManagement.Controllers
{
    [Authorize]
    public class CustomerController : ElephantController
    {
        private readonly IMyEmailSender _emailSender;
        private readonly CateringContext _context;

        public CustomerController(CateringContext context, IMyEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        // GET: Customers
        public async Task<IActionResult> Index(DateTime StartDate, DateTime EndDate, int? page, int? pageSizeID,
            string actionButton, string sortDirection = "asc", string sortField = "Customer")
        {
            //List of sort options.
            //NOTE: make sure this array has matching values to the column headings
            string[] sortOptions = new[] { "Customer", "Company Name", "Phone", "Customer Code" };

            var customers = _context.Customers
                .Include(p => p.CustomerThumbnail)
                .AsNoTracking();

            //Before we sort, see if we have called for a change of filtering or sorting
            if (EndDate == DateTime.MinValue)
            {
                StartDate = _context.Functions.Min(o => o.StartTime).Date;
                EndDate = _context.Functions.Max(o => o.StartTime).Date;
                ViewData["StartDate"] = StartDate.ToString("yyyy-MM-dd");
                ViewData["EndDate"] = EndDate.ToString("yyyy-MM-dd");
            }
            //Check the order of the dates and swap them if required
            if (EndDate < StartDate)
            {
                DateTime temp = EndDate;
                EndDate = StartDate;
                StartDate = temp;
            }
            if (!String.IsNullOrEmpty(actionButton)) //Form Submitted!
            {
                page = 1;//Reset page to start

                if (sortOptions.Contains(actionButton))//Change of sort is requested
                {
                    if (actionButton == sortField) //Reverse order on same field
                    {
                        sortDirection = sortDirection == "asc" ? "desc" : "asc";
                    }
                    sortField = actionButton;//Sort by the button clicked
                }
            }
            //Now we know which field and direction to sort by
            if (sortField == "Company Name")
            {
                if (sortDirection == "asc")
                {
                    customers = customers
                        .OrderBy(p => p.CompanyName);
                }
                else
                {
                    customers = customers
                        .OrderByDescending(p => p.CompanyName);
                }
            }
            else if (sortField == "Phone")
            {
                if (sortDirection == "asc")
                {
                    customers = customers
                        .OrderBy(p => p.Phone);
                }
                else
                {
                    customers = customers
                        .OrderByDescending(p => p.Phone);
                }
            }
            else if (sortField == "Customer Code")
            {
                if (sortDirection == "asc")
                {
                    customers = customers
                        .OrderBy(p => p.CustomerCode);
                }
                else
                {
                    customers = customers
                        .OrderByDescending(p => p.CustomerCode);
                }
            }
            else //Sorting by Customer
            {
                if (sortDirection == "asc")
                {
                    customers = customers
                        .OrderBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
                else
                {
                    customers = customers
                        .OrderByDescending(p => p.LastName)
                        .ThenByDescending(p => p.FirstName);
                }
            }
            //Set sort for next time
            ViewData["sortField"] = sortField;
            ViewData["sortDirection"] = sortDirection;

            //Handle Paging
            int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, ControllerName());
            ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);
            var pagedData = await PaginatedList<Customer>.CreateAsync(customers.AsNoTracking(), page ?? 1, pageSize);

            return View(pagedData);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Customers == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Functions)
                .Include(p => p.CustomerPhoto)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Customers/Create
        [Authorize(Roles = "Admin, Supervisor, Staff")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Supervisor, Staff")]
        public async Task<IActionResult> Create([Bind("ID,FirstName,MiddleName,LastName,CompanyName," +
            "Phone,Email,CustomerCode")] Customer customer, IFormFile thePicture)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    await AddPicture(customer, thePicture);
                    _context.Add(customer);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Index", "CustomerFunction", new { CustomerID = customer.ID });
                }
            }
            catch (DbUpdateException dex)
            {
                if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed: Customers.CustomerCode"))
                {
                    ModelState.AddModelError("CustomerCode", "Unable to save changes. Remember, you cannot have duplicate Customer Codes.");
                }
                else
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }

            return View(customer);
        }

        // GET: Customers/Edit/5
        [Authorize(Roles = "Admin, Supervisor,Staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Customers == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(p => p.CustomerPhoto)
                .FirstOrDefaultAsync(p => p.ID == id);
            if (customer == null)
            {
                return NotFound();
            }

            if (User.IsInRole("Staff"))
            {
                if (customer.UpdatedBy != User.Identity.Name)
                {
                    ModelState.AddModelError("", "As a Staff, you cannot edit this " +
                        "Customer because you did not enter them into the system.");
                    ViewData["NoSubmit"] = "disabled=disabled";
                }
            }

            return View(customer);
        }

        // POST: Customers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Supervisor, Staff")]
        public async Task<IActionResult> Edit(int id, string chkRemoveImage, IFormFile thePicture)
        {
            //Go get the customer to update
            var customerToUpdate = await _context.Customers
                .Include(p => p.CustomerPhoto)
                .FirstOrDefaultAsync(p => p.ID == id);

            //Check that we got the customer or exit with a not found error
            if (customerToUpdate == null)
            {
                return NotFound();
            }

            if (User.IsInRole("Staff"))
            {
                if (customerToUpdate.CreatedBy != User.Identity.Name)
                {
                    ModelState.AddModelError("", "As a Staff, you cannot delete this " +
                        "Customer because you did not enter them into the system.");
                    ViewData["NoSubmit"] = "disabled=disabled";

                    return View(customerToUpdate);//This line prevents the attempt to delete
                }
            }

            //Try updating it with the values posted
            if (await TryUpdateModelAsync<Customer>(customerToUpdate, "",
                c => c.FirstName, c => c.MiddleName, c => c.LastName, c => c.CompanyName,
                c => c.Phone, c => c.Email, c => c.CustomerCode))
            {
                try
                {


                    //For the image
                    if (chkRemoveImage != null)
                    {
                        //If we are just deleting the two versions of the photo, we need to make sure the Change Tracker knows
                        //about them both so go get the Thumbnail since we did not include it.
                        customerToUpdate.CustomerThumbnail = _context.CustomerThumbnails.Where(p => p.CustomerID == customerToUpdate.ID).FirstOrDefault();
                        //Then, setting them to null will cause them to be deleted from the database.
                        customerToUpdate.CustomerPhoto = null;
                        customerToUpdate.CustomerThumbnail = null;
                    }
                    else
                    {
                        await AddPicture(customerToUpdate, thePicture);
                    }
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Index", "CustomerFunction", new { CustomerID = customerToUpdate.ID });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customerToUpdate.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException dex)
                {
                    if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed: Customers.CustomerCode"))
                    {
                        ModelState.AddModelError("CustomerCode", "Unable to save changes. Remember, you cannot have duplicate Customer Codes.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                    }
                }
            }
            return View(customerToUpdate);
        }

        // GET: Customers/Delete/5
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Customers == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Supervisor")]

        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Customers == null)
            {
                return Problem("There are no Customers to delete.");
            }
            var customer = await _context.Customers.FindAsync(id);
            try
            {
                if (customer != null)
                {
                    _context.Customers.Remove(customer);
                }
                await _context.SaveChangesAsync();
                return Redirect(ViewData["returnURL"].ToString());
            }
            catch (DbUpdateException dex)
            {
                if (dex.GetBaseException().Message.Contains("FOREIGN KEY constraint failed"))
                {
                    ModelState.AddModelError("", "Unable to Delete Customer. Remember, you cannot delete a Customer that has a function in the system.");
                }
                else
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }
            return View(customer);

        }

        private async Task AddPicture(Customer customer, IFormFile thePicture)
        {
            //Get the picture and save it with the Customer (2 sizes)
            if (thePicture != null)
            {
                string mimeType = thePicture.ContentType;
                long fileLength = thePicture.Length;
                if (!(mimeType == "" || fileLength == 0))//Looks like we have a file!!!
                {
                    if (mimeType.Contains("image"))
                    {
                        using var memoryStream = new MemoryStream();
                        await thePicture.CopyToAsync(memoryStream);
                        var pictureArray = memoryStream.ToArray();//Gives us the Byte[]

                        //Check if we are replacing or creating new
                        if (customer.CustomerPhoto != null)
                        {
                            //We already have pictures so just replace the Byte[]
                            customer.CustomerPhoto.Content = ResizeImage.shrinkImageWebp(pictureArray, 500, 600);

                            //Get the Thumbnail so we can update it.  Remember we didn't include it
                            customer.CustomerThumbnail = _context.CustomerThumbnails.Where(p => p.CustomerID == customer.ID).FirstOrDefault();
                            customer.CustomerThumbnail.Content = ResizeImage.shrinkImageWebp(pictureArray, 75, 90);
                        }
                        else //No pictures saved so start new
                        {
                            customer.CustomerPhoto = new CustomerPhoto
                            {
                                Content = ResizeImage.shrinkImageWebp(pictureArray, 500, 600),
                                MimeType = "image/webp"
                            };
                            customer.CustomerThumbnail = new CustomerThumbnail
                            {
                                Content = ResizeImage.shrinkImageWebp(pictureArray, 75, 90),
                                MimeType = "image/webp"
                            };
                        }
                    }
                }
            }
        }


        private void PopulateAssignedCustomerData()
        {
            //this is for customer list who has email 
            var allOptions = (from c in _context.Customers
                              where !string.IsNullOrEmpty(c.Email)
                              orderby c.FirstName
                              select c
                              ).ToList();

            //var allOptions = _context.Customers;

            //Instead of one list with a boolean, we will make two lists
            var selected = new List<ListOptionVM>();
            var available = new List<ListOptionVM>();

            foreach (var c in allOptions)
            {
               
                    available.Add(new ListOptionVM
                    {
                        ID = c.ID,
                        DisplayText = c.FullName + " - " + c.Email
                    });
                
            }

            ViewData["selOpts"] = new MultiSelectList(selected.OrderBy(s => s.DisplayText), "ID", "DisplayText");
            ViewData["availOpts"] = new MultiSelectList(available.OrderBy(s => s.DisplayText), "ID", "DisplayText");
        }

        // GET/POST: Customer/Notification/5
        public async Task<IActionResult> Notification(string[] selectedOptions, string Subject, string emailContent)
        {

            PopulateAssignedCustomerData();

            if (string.IsNullOrEmpty(Subject) || string.IsNullOrEmpty(emailContent))
            {
                ViewData["Message"] = "You must enter both a Subject and some message Content before sending the message.";
            }
            else
            {
                int customerCount = 0;
                try
                {
                    //Send a Notice.
                    List<EmailAddress> selectedCustomer = _context.Customers
                        .Where(c => selectedOptions.Contains(c.ID.ToString()))
                        .Select(c => new EmailAddress { ID = c.ID, Address = c.Email })
                        .ToList();

                    Console.WriteLine("Selected Customers: " + string.Join(", ", selectedCustomer.Select(c => c.Address)));

                    customerCount = selectedCustomer.Count;

                    if (customerCount > 0)
                    {
                        var msg = new EmailMessage()
                        {
                            ToAddresses = selectedCustomer,
                            Subject = Subject,
                            Content = "<p>" + emailContent + "</p><p>Please access the <strong>Niagara College</strong> web site to review.</p>"
                        };
                        await _emailSender.SendToManyAsync(msg);
                        ViewData["Message"] = "Message sent to " + customerCount + " Customer"
                            + ((customerCount == 1) ? "." : "s.");
                    }
                    else
                    {
                        ViewData["Message"] = "Message NOT sent!  No Customer selected.";
                    }
                }
                catch (Exception ex)
                {
                    string errMsg = ex.GetBaseException().Message;
                    ViewData["Message"] = "Error: Could not send email message to the " + customerCount + " Customer"
                        + ((customerCount == 1) ? "" : "s") + " in the Customer list.";
                }
            }
            return View();
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.ID == id);
        }
    }
}
