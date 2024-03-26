using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CateringManagement.Data;
using CateringManagement.Models;
using CateringManagement.CustomControllers;
using System.Numerics;
using Microsoft.AspNetCore.Authorization;

namespace CateringManagement.Controllers
{
    public class WorkerController : LookupsController
    {
        private readonly CateringContext _context;

        public WorkerController(CateringContext context)
        {
            _context = context;
        }

        // GET: Worker
        [Authorize(Roles = "Admin, Supervisor")]
        public IActionResult Index()
        {
            return Redirect(ViewData["returnURL"].ToString());
        }

        // GET: Worker/Create
        [Authorize(Roles = "Admin, Supervisor")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Worker/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> Create([Bind("ID,FirstName,MiddleName,LastName,Phone")] Worker worker)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(worker);
                    await _context.SaveChangesAsync();
                    return Redirect(ViewData["returnURL"].ToString());
                }
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }
            return View(worker);

        }

        // GET: Worker/Edit/5
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Workers == null)
            {
                return NotFound();
            }

            var worker = await _context.Workers.FindAsync(id);
            if (worker == null)
            {
                return NotFound();
            }
            return View(worker);
        }

        // POST: Worker/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> Edit(int id)
        {
            //Go get the Worker to update
            var workerToUpdate = await _context.Workers.FirstOrDefaultAsync(p => p.ID == id);

            //Check that you got it or exit with a not found error
            if (workerToUpdate == null)
            {
                return NotFound();
            }

            //Try updating it with the values posted
            if (await TryUpdateModelAsync<Worker>(workerToUpdate, "",
                d => d.FirstName, d => d.MiddleName, d => d.LastName, d => d.Phone))
            {
                try
                {
                    await _context.SaveChangesAsync();
                    return Redirect(ViewData["returnURL"].ToString());
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!WorkerExists(workerToUpdate.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }
            return View(workerToUpdate);
        }

        // GET: Worker/Delete/5
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Workers == null)
            {
                return NotFound();
            }

            var worker = await _context.Workers
                .FirstOrDefaultAsync(m => m.ID == id);
            if (worker == null)
            {
                return NotFound();
            }

            return View(worker);
        }

        // POST: Worker/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Workers == null)
            {
                return Problem("There are no Workers to delete.");
            }
            var worker = await _context.Workers.FindAsync(id);
            try
            {
                if (worker != null)
                {
                    _context.Workers.Remove(worker);
                }
                await _context.SaveChangesAsync();
                return Redirect(ViewData["returnURL"].ToString());
            }
            catch (DbUpdateException dex)
            {
                if (dex.GetBaseException().Message.Contains("FOREIGN KEY constraint failed"))
                {
                    ModelState.AddModelError("", "Unable to Delete Worker. Remember, you cannot delete a Worker that is used in the system.");
                }
                else
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }

            return View(worker);
        }

        private bool WorkerExists(int id)
        {
            return _context.Workers.Any(e => e.ID == id);
        }
    }
}
