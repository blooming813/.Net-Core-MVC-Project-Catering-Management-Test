using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CateringManagement.Data;
using CateringManagement.Models;
using Microsoft.AspNetCore.Authorization;
using CateringManagement.CustomControllers;
using CateringManagement.Utilities;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Function = CateringManagement.Models.Function;
using Microsoft.EntityFrameworkCore.Storage;
using String = System.String;
using CateringManagement.ViewModels;

namespace CateringManagement.Controllers
{
    [Authorize(Roles = "Admin, Supervisor, Staff")]

    public class CustomerFunctionController : ElephantController
    {
        private readonly CateringContext _context;

        public CustomerFunctionController(CateringContext context)
        {
            _context = context;
        }

        // GET: CustomerFunctions
        public async Task<IActionResult> Index(DateTime StartDate, DateTime EndDate, 
            int? CustomerID, int? FunctionTypeID, string SearchString,
            int? page, int? pageSizeID, string actionButton, string sortDirection = "asc", 
            string sortField = "Function")
        {
            ViewData["returnURL"] = MaintainURL.ReturnURL(HttpContext, "Customer");

            if (!CustomerID.HasValue)
            {
                return Redirect(ViewData["returnURL"].ToString());
            }
            //Count the number of filters applied - start by assuming no filters
            ViewData["Filtering"] = "btn-outline-secondary";
            int numberFilters = 0;
            PopulateDropDownLists();
            //List of sort options.
            //NOTE: make sure this array has matching values to the column headings
            string[] sortOptions = new[] { "Function", "Guar. No."};

            if(EndDate == DateTime.MinValue)
            {
                StartDate = _context.Functions.Min(d => d.StartTime).Date;
                EndDate = _context.Functions.Max(d => d.StartTime).Date;
                ViewData["StartDate"] = StartDate.ToString("yyyy-MM-dd");
                ViewData["EndDate"] = EndDate.ToString("yyyy-MM-dd");
            }
            if (EndDate < StartDate)
            {
                DateTime temp = EndDate;
                EndDate = StartDate;
                StartDate = temp;
            }
            var functions = from f in _context.Functions
                    .Include(f => f.Customer)
                    .Include(f => f.FunctionType)
                    .Include(f => f.MealType)
                    .Include(f => f.FunctionRooms).ThenInclude(fr => fr.Room)
                    .Include(f => f.FunctionDocuments)
                                            where f.CustomerID == CustomerID.GetValueOrDefault() &&
                                                  f.StartTime >= StartDate && f.StartTime <= EndDate.AddDays(1)
                                            select f;
            //var functions = from f in _context.Functions
            //   .Include(f => f.Customer)
            //   .Include(f => f.FunctionType)
            //   .Include(f => f.MealType)
            //   .Include(f => f.FunctionRooms).ThenInclude(fr => fr.Room)
            //   .Include(f => f.FunctionDocuments)
            //    where f.CustomerID == CustomerID.GetValueOrDefault() 
            //    select f;
            //Add as many filters as needed
            if (FunctionTypeID.HasValue)
            {
                functions = functions.Where(p => p.FunctionTypeID == FunctionTypeID);
                numberFilters++;
            }
            if (!System.String.IsNullOrEmpty(SearchString))
            {
                functions = functions.Where(p => p.Name.ToUpper().Contains(SearchString.ToUpper())
                                       || p.LobbySign.ToUpper().Contains(SearchString.ToUpper()));
                numberFilters++;
            }
            //Give feedback about the state of the filters
            if (numberFilters != 0)
            {
                //Toggle the Open/Closed state of the collapse depending on if we are filtering
                ViewData["Filtering"] = " btn-danger";
                //Show how many filters have been applied
                ViewData["numberFilters"] = "(" + numberFilters.ToString()
                    + " Filter" + (numberFilters > 1 ? "s" : "") + " Applied)";
                //Keep the Bootstrap collapse open
                //@ViewData["ShowFilter"] = " show";
            }
            //Before we sort, see if we have called for a change of filtering or sorting
            if (!System.String.IsNullOrEmpty(actionButton)) //Form Submitted!
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
            if (sortField == "Guar. No.")
            {
                if (sortDirection == "asc")
                {
                    functions = functions
                        .OrderBy(p => p.GuaranteedNumber);
                }
                else
                {
                    functions = functions
                        .OrderByDescending(p => p.GuaranteedNumber);
                }
            }
            else //Sorting by Function Date
            {
                if (sortDirection == "asc")
                {
                    functions = functions
                        .OrderBy(p => p.StartTime);
                }
                else
                {
                    functions = functions
                        .OrderByDescending(p => p.StartTime);
                }
            }
            //Set sort for next time
            ViewData["sortField"] = sortField;
            ViewData["sortDirection"] = sortDirection;

            //get the Master record, the customer, so it can be displayed at the top of the screen
            Customer customer = await _context.Customers
               .Include(p => p.CustomerThumbnail)
               .Where(c=>c.ID == CustomerID.GetValueOrDefault())
               .AsNoTracking()
               .FirstOrDefaultAsync();

            ViewBag.Customer = customer;

            //Handle Paging
            int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, ControllerName());
            ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);
            var pagedData = await PaginatedList<Models.Function>.CreateAsync(functions.AsNoTracking(), page ?? 1, pageSize);

            return View(pagedData);
        }

        // GET: CustomerFunctions/Details/5
        //public async Task<IActionResult> Details(int? id)
        //{
        //    if (id == null || _context.Functions == null)
        //    {
        //        return NotFound();
        //    }

        //    var function = await _context.Functions
        //        .Include(f => f.Customer)
        //        .Include(f => f.FunctionType)
        //        .Include(f => f.MealType)
        //        .FirstOrDefaultAsync(m => m.ID == id);
        //    if (function == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(function);
        //}

        // GET: CustomerFunctions/Create
        public IActionResult Add(int? CustomerID, string CustomerName)
        {
            if (!CustomerID.HasValue)
            {
                return Redirect(ViewData["returnURL"].ToString());
            }
            ViewData["CustomerName"] = CustomerName;
            Function f = new Function()
            {
                CustomerID = CustomerID.GetValueOrDefault()
            };
            PopulateDropDownLists(f);
            PopulateAssignedRoomCheckboxes(f);
            return View(f);
        }

        // POST: CustomerFunctions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([Bind("ID,Name,LobbySign,StartTime,EndTime,SetupNotes,BaseCharge,PerPersonCharge," +
            "GuaranteedNumber,SOCAN,Deposit,Alcohol,DepositPaid,NoHST,NoGratuity,CustomerID,FunctionTypeID,MealTypeID")] Models.Function function, string CustomerName)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(function);
                    await _context.SaveChangesAsync();
                    return Redirect(ViewData["returnURL"].ToString());
                }
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }

            PopulateDropDownLists(function);
            ViewData["CustomerName"] = CustomerName;
            return View(function);
        }

        // GET: CustomerFunctions/Edit/5
        public async Task<IActionResult> Update(int? id)
        {
            if (id == null || _context.Functions == null)
            {
                return NotFound();
            }

            var function = await _context.Functions
                .Include(f => f.Customer)
                .Include(f => f.FunctionRooms).ThenInclude(fr => fr.Room)
                .Include(f => f.FunctionDocuments)
                .FirstOrDefaultAsync(f => f.ID == id);

            if (function == null)
            {
                return NotFound();
            }

            PopulateDropDownLists();
            PopulateAssignedRoomLists(function);
            return View(function);
        }

        // POST: CustomerFunctions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, Byte[] RowVersion,
            string[] selectedOptions, List<IFormFile> theFiles)
        {
            // Go get the function to update
            var functionToUpdate = await _context.Functions
                .Include(f => f.Customer)
                .Include(f => f.FunctionRooms).ThenInclude(fr => fr.Room)
                .Include(f => f.FunctionDocuments)
                .FirstOrDefaultAsync(f => f.ID == id);

            // Check that we got the function or exit with a not found error
            if (functionToUpdate == null)
            {
                return NotFound();
            }

            UpdateFunctionRoomsListboxes(selectedOptions, functionToUpdate);

            if (await TryUpdateModelAsync<Function>(functionToUpdate, "",
                 f => f.Name, f => f.LobbySign, f => f.StartTime, f => f.EndTime, f => f.SetupNotes,
                 f => f.BaseCharge, f => f.PerPersonCharge, f => f.GuaranteedNumber,
                 f => f.SOCAN, f => f.Deposit, f => f.DepositPaid, f => f.NoHST,
                 f => f.NoGratuity, f => f.Alcohol, f => f.MealTypeID,
                 f => f.CustomerID, f => f.FunctionTypeID))
            {
                try
                {
                    await AddDocumentsAsync(functionToUpdate, theFiles);
                    await _context.SaveChangesAsync();
                    return Redirect(ViewData["returnURL"].ToString());
                }
                catch (RetryLimitExceededException /* dex */)
                {
                    ModelState.AddModelError("", "Unable to save changes after multiple attempts. Try again, and if the problem persists, see your system administrator.");
                }
                catch (DbUpdateConcurrencyException ex)// Added for concurrency
                {
                    var exceptionEntry = ex.Entries.Single();
                    var clientValues = (Function)exceptionEntry.Entity;
                    var databaseEntry = exceptionEntry.GetDatabaseValues();
                    if (databaseEntry == null)
                    {
                        ModelState.AddModelError("",
                            "Unable to save changes. The Function was deleted by another user.");
                    }
                    else
                    {
                        var databaseValues = (Function)databaseEntry.ToObject();
                        if (databaseValues.Name != clientValues.Name)
                            ModelState.AddModelError("Name", "Current value: "
                                + databaseValues.Name);
                        if (databaseValues.LobbySign != clientValues.LobbySign)
                            ModelState.AddModelError("LobbySign", "Current value: "
                                + databaseValues.LobbySign);
                        if (databaseValues.SetupNotes != clientValues.SetupNotes)
                            ModelState.AddModelError("SetupNotes", "Current value: "
                                + databaseValues.SetupNotes);
                        if (databaseValues.StartTime != clientValues.StartTime)
                            ModelState.AddModelError("StartTime", "Current value: "
                                + String.Format("{0:F}", databaseValues.StartTime));
                        if (databaseValues.EndTime != clientValues.EndTime)
                            ModelState.AddModelError("EndTime", "Current value: "
                                + String.Format("{0:F}", databaseValues.EndTime));
                        if (databaseValues.BaseCharge != clientValues.BaseCharge)
                            ModelState.AddModelError("BaseCharge", "Current value: "
                                + String.Format("{0:c}", databaseValues.BaseCharge));
                        if (databaseValues.PerPersonCharge != clientValues.PerPersonCharge)
                            ModelState.AddModelError("PerPersonCharge", "Current value: "
                                + String.Format("{0:c}", databaseValues.PerPersonCharge));
                        if (databaseValues.GuaranteedNumber != clientValues.GuaranteedNumber)
                            ModelState.AddModelError("GuaranteedNumber", "Current value: "
                                + databaseValues.GuaranteedNumber);
                        if (databaseValues.SOCAN != clientValues.SOCAN)
                            ModelState.AddModelError("SOCAN", "Current value: "
                                + String.Format("{0:c}", databaseValues.SOCAN));
                        if (databaseValues.Deposit != clientValues.Deposit)
                            ModelState.AddModelError("Deposit", "Current value: "
                                + String.Format("{0:c}", databaseValues.Deposit));
                        if (databaseValues.Alcohol != clientValues.Alcohol)
                            ModelState.AddModelError("Alcohol", "Current value: "
                                + databaseValues.Alcohol.ToString());
                        if (databaseValues.DepositPaid != clientValues.DepositPaid)
                            ModelState.AddModelError("DepositPaid", "Current value: "
                                + databaseValues.DepositPaid.ToString());
                        if (databaseValues.NoHST != clientValues.NoHST)
                            ModelState.AddModelError("NoHST", "Current value: "
                                + databaseValues.NoHST.ToString());
                        if (databaseValues.NoGratuity != clientValues.NoGratuity)
                            ModelState.AddModelError("NoGratuity", "Current value: "
                                + databaseValues.NoGratuity.ToString());
                        //For the foreign key, we need to go to the database to get the information to show
                        if (databaseValues.CustomerID != clientValues.CustomerID)
                        {
                            Customer databaseCustomer = await _context.Customers.FirstOrDefaultAsync(i => i.ID == databaseValues.CustomerID);
                            ModelState.AddModelError("CustomerID", $"Current value: {databaseCustomer?.FullName}");
                        }
                        if (databaseValues.FunctionTypeID != clientValues.FunctionTypeID)
                        {
                            FunctionType databaseFunctionType = await _context.FunctionTypes.FirstOrDefaultAsync(i => i.ID == databaseValues.FunctionTypeID);
                            ModelState.AddModelError("FunctionTypeID", $"Current value: {databaseFunctionType?.Name}");
                        }
                        //A little extra work for the nullable foreign key.  No sense going to the database and asking for something
                        //we already know is not there.
                        if (databaseValues.MealTypeID != clientValues.MealTypeID)
                        {
                            if (databaseValues.MealTypeID.HasValue)
                            {
                                MealType databaseMealType = await _context.MealTypes.FirstOrDefaultAsync(i => i.ID == databaseValues.MealTypeID);
                                ModelState.AddModelError("MealTypeID", $"Current value: {databaseMealType?.Name}");
                            }
                            else

                            {
                                ModelState.AddModelError("MealTypeID", $"Current value: No Food Service");
                            }
                        }
                        ModelState.AddModelError(string.Empty, "The record you attempted to edit "
                                + "was modified by another user after you received your values. The "
                                + "edit operation was canceled and the current values in the database "
                                + "have been displayed. If you still want to save your version of this record, click "
                                + "the Save button again. Otherwise click the 'Back to Function List' hyperlink.");
                        functionToUpdate.RowVersion = (byte[])databaseValues.RowVersion;
                        ModelState.Remove("RowVersion");
                    }
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }
            PopulateDropDownLists(functionToUpdate);

            return View(functionToUpdate);
        }

        // GET: CustomerFunctions/Delete/5
        public async Task<IActionResult> Remove(int? id)
        {
            if (id == null || _context.Functions == null)
            {
                return NotFound();
            }

            var function = await _context.Functions
              .Include(f => f.Customer)
              .Include(f => f.FunctionType)
              .Include(f => f.FunctionRooms).ThenInclude(fr => fr.Room)
              .AsNoTracking()
              .FirstOrDefaultAsync(m => m.ID == id);

            if (function == null)
            {
                return NotFound();
            }

            if(User.IsInRole("Supervisor"))
            {
                if (function.CreatedBy != User.Identity.Name)
                {
                    ModelState.AddModelError("", "As a Supervisor, you cannot delete this " +
                        "Function because you did not enter them into the system.");
                    ViewData["NoSubmit"] = "disabled=disabled";
                }
            }

            return View(function);
        }

        // POST: CustomerFunctions/Delete/5
        [HttpPost, ActionName("Remove")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> RemoveConfirmed(int id)
        {
            if (_context.Functions == null)
            {
                return Problem("There are no Functions to delete.");
            }
            var function = await _context.Functions
              //.Include(f => f.Customer)
              .Include(f => f.FunctionType)
              .Include(f => f.FunctionRooms).ThenInclude(fr => fr.Room)
              .FirstOrDefaultAsync(m => m.ID == id);

            if (User.IsInRole("Supervisor"))
            {
                if (function.CreatedBy != User.Identity.Name)
                {
                    ModelState.AddModelError("", "As a Supervisor, you cannot delete this " +
                        "Function because you did not enter them into the system.");
                    ViewData["NoSubmit"] = "disabled=disabled";

                    return View(function);//This line prevents the attempt to delete
                }
            }

            try
            {
                if (function != null)
                {
                    _context.Functions.Remove(function);
                }
                await _context.SaveChangesAsync();
                return Redirect(ViewData["returnURL"].ToString());
            }
            catch (DbUpdateException)
            {
                //Note: there is really no reason a delete should fail if you can "talk" to the database.
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }

            return View(function);
        }
        private SelectList FunctionTypeList(int? selectedId)
        {
            return new SelectList(_context
                .FunctionTypes
                .OrderBy(m => m.Name), "ID", "Name", selectedId);
        }
        private SelectList MealTypeList(int? selectedId)
        {
            return new SelectList(_context
                .MealTypes
                .OrderBy(m => m.Name), "ID", "Name", selectedId);
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
        private void PopulateDropDownLists(Models.Function funtion = null)
        {
            ViewData["FunctionTypeID"] = FunctionTypeList(funtion?.FunctionTypeID);
            ViewData["MealTypeID"] = MealTypeList(funtion?.MealTypeID);
            //ViewData["FunctionID"] = FunctionSelectList(funtion?.ID);
        }
        private async Task AddDocumentsAsync(Function function, List<IFormFile> theFiles)
        {
            foreach (var f in theFiles)
            {
                if (f != null)
                {
                    string mimeType = f.ContentType;
                    string fileName = Path.GetFileName(f.FileName);
                    long fileLength = f.Length;
                    //Note: you could filter for mime types if you only want to allow
                    //certain types of files.  I am allowing everything.
                    if (!(fileName == "" || fileLength == 0))//Looks like we have a file!!!
                    {
                        FunctionDocument d = new FunctionDocument();
                        using (var memoryStream = new MemoryStream())
                        {
                            await f.CopyToAsync(memoryStream);
                            d.FileContent.Content = memoryStream.ToArray();
                        }
                        d.MimeType = mimeType;
                        d.FileName = fileName;
                        function.FunctionDocuments.Add(d);
                    };
                }
            }
        }
        private void UpdateFunctionRoomsListboxes(string[] selectedOptions, Function functionToUpdate)
        {
            if (selectedOptions == null)
            {
                functionToUpdate.FunctionRooms = new List<FunctionRoom>();
                return;
            }

            var selectedOptionsHS = new HashSet<string>(selectedOptions);
            var currentOptionsHS = new HashSet<int>(functionToUpdate.FunctionRooms.Select(b => b.RoomID));
            foreach (var r in _context.Rooms)
            {
                if (selectedOptionsHS.Contains(r.ID.ToString()))//it is selected
                {
                    if (!currentOptionsHS.Contains(r.ID))//but not currently in the Function's collection - Add it!
                    {
                        functionToUpdate.FunctionRooms.Add(new FunctionRoom
                        {
                            RoomID = r.ID,
                            FunctionID = functionToUpdate.ID
                        });
                    }
                }
                else //not selected
                {
                    if (currentOptionsHS.Contains(r.ID))//but is currently in the Function's collection - Remove it!
                    {
                        FunctionRoom roomToRemove = functionToUpdate.FunctionRooms.FirstOrDefault(d => d.RoomID == r.ID);
                        _context.Remove(roomToRemove);
                    }
                }
            }
        }
        private void PopulateAssignedRoomCheckboxes(Function function)
        {
            //For this to work, you must have Included the FunctionRooms 
            //in the Function
            var allOptions = _context.Rooms;
            var currentOptionIDs = new HashSet<int>(function.FunctionRooms.Select(b => b.RoomID));
            var checkBoxes = new List<CheckOptionVM>();
            foreach (var option in allOptions)
            {
                checkBoxes.Add(new CheckOptionVM
                {
                    ID = option.ID,
                    DisplayText = option.Summary,
                    Assigned = currentOptionIDs.Contains(option.ID)
                });
            }
            ViewData["RoomOptions"] = checkBoxes;
        }
        private void PopulateAssignedRoomLists(Function function)
        {
            //For this to work, you must have Included the child collection in the parent object
            var allOptions = _context.Rooms;
            var currentOptionsHS = new HashSet<int>(function.FunctionRooms.Select(b => b.RoomID));
            //Instead of one list with a boolean, we will make two lists
            var selected = new List<ListOptionVM>();
            var available = new List<ListOptionVM>();
            foreach (var r in allOptions)
            {
                if (currentOptionsHS.Contains(r.ID))
                {
                    selected.Add(new ListOptionVM
                    {
                        ID = r.ID,
                        DisplayText = r.Summary
                    });
                }
                else
                {
                    available.Add(new ListOptionVM
                    {
                        ID = r.ID,
                        DisplayText = r.Summary
                    });
                }
            }

            ViewData["selOpts"] = new MultiSelectList(selected.OrderBy(s => s.DisplayText), "ID", "DisplayText");
            ViewData["availOpts"] = new MultiSelectList(available.OrderBy(s => s.DisplayText), "ID", "DisplayText");
        }

        [HttpGet]
        public JsonResult GetMealTypes(int? id)
        {
            return Json(MealTypeList(id));
        }
        private bool FunctionExists(int id)
        {
          return _context.Functions.Any(e => e.ID == id);
        }
    }
}
