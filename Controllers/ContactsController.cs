using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ContactMVC.Data;
using ContactMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using ContactMVC.Enums;
using ContactMVC.Services.Interfaces;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace ContactMVC.Controllers
{
    public class ContactsController : Controller
    {
        // injection
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IImageService _imageService;
        private readonly IAddressBookService _addressBookService;
        private readonly IEmailSender _emailSender;

        public ContactsController(ApplicationDbContext context,
                                    UserManager<AppUser> userManager,
                                    IImageService imageService,
                                    IAddressBookService addressBookService,
                                    IEmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _imageService = imageService;
            _addressBookService = addressBookService;
            _emailSender = emailSender;
        }

        // GET: Contacts
        [Authorize]
        public async Task<IActionResult> Index(int? categoryId, string? swalMessage = null)
        {
            string userId = _userManager.GetUserId(User);

            ViewData["SwalMessage"] = swalMessage;

            // TODO: add instance of AppUser

            List<Contact> contacts = await _context.Contacts
                                    .Where(c => c.AppUserId == userId)
                                    .Include(c => c.AppUser)
                                    .Include(c => c.Categories)
                                    .ToListAsync();

            // TODO: get Categories from the AppUser based on whether they have chosen a Category to "filter" by
            List<Category> userCategories = await _context.Categories.Where(c => c.AppUserId == userId)
                                                                    .ToListAsync();

            if (categoryId == null)
            {
                contacts = await _context.Contacts
                                        .Where(c => c.AppUserId == userId)
                                        .Include(c => c.AppUser)
                                        .Include(c => c.Categories)
                                        .ToListAsync();
            } else
            {
                contacts = (await _context.Categories
                                            .Include(c => c.Contacts)
                                            .FirstOrDefaultAsync(c => c.AppUserId == userId && c.Id == categoryId))!.Contacts.ToList();
            }

            // TODO: update MultiSelect instance to include selected Category (if one has been selected)
            ViewData["CategoryId"] = new SelectList(userCategories, "Id", "Name", categoryId);

            return View(contacts);
        }

        // TODO: add method/action to Search for Contacts based on form in Index and return the results directly to Index view
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SearchContacts(string searchString)
        {
            string appUserId = _userManager.GetUserId(User);

            List<Contact> contacts = new List<Contact>();

            AppUser? appUser = await _context.Users
                                        .Include(c => c.Contacts)
                                        .ThenInclude(c => c.Categories)
                                        .FirstOrDefaultAsync(u => u.Id == appUserId);

            if (string.IsNullOrEmpty(searchString))
            {
                contacts = appUser!.Contacts
                                    .OrderBy(c => c.LastName)
                                    .ThenBy(c => c.FirstName)
                                    .ToList();
            } 
            else
            {
                contacts = appUser!.Contacts
                                   .Where(c => c.FullName!.ToLower().Contains(searchString.ToLower()))
                                   .OrderBy(c => c.LastName)
                                   .ThenBy(c => c.FirstName)
                                   .ToList();
            }

            ViewData["CategoryId"] = new SelectList(appUser.Categories, "Id", "Name");

            return View(nameof(Index), contacts);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EmailContact(int? id)
        {
            string appUserId = _userManager.GetUserId(User);

            Contact? contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == id && c.AppUserId == appUserId);

            if (contact == null)
            {
                return NotFound();
            }

            EmailData emailData = new EmailData()
            {
                EmailAddress = contact.Email,
                FirstName = contact.FirstName,
                LastName = contact.LastName
            };

            EmailContactViewModel viewModel = new EmailContactViewModel()
            {
                Contact = contact,
                EmailData = emailData
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EmailContact(EmailContactViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                string? swalMessage = string.Empty;

                try
                {
                    await _emailSender.SendEmailAsync(viewModel.EmailData!.EmailAddress, viewModel.EmailData.EmailSubject, viewModel.EmailData.EmailBody);

                    swalMessage = "Success: Email Sent!";

                    return RedirectToAction("Index", "Contacts", new { swalMessage });

                }
                catch (Exception)
                {
                    swalMessage = "Error: Email Send Failed";
                    return RedirectToAction("Index", "Contacts", new { swalMessage });
                    throw;
                }
            }

            return View();
        }

        // GET: Contacts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Contacts == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts
                .Include(c => c.AppUser)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null)
            {
                return NotFound();
            }

            return View(contact);
        }

        // GET: Contacts/Create
        [Authorize]
        public async Task<IActionResult> Create()
        {
            ViewData["StatesList"] = new SelectList(Enum.GetValues(typeof(States)).Cast<States>().ToList());

            /* TODO: Categories drop down */
            string userId = _userManager.GetUserId(User);
            List<Category> categories = await _context.Categories
                                            .Where(c => c.AppUserId == userId)
                                            .ToListAsync();

            ViewData["CategoryList"] = new MultiSelectList(categories, "Id", "Name");

            return View();
        }

        // POST: Contacts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FirstName,LastName,BirthDate,Address1,Address2,City,State,ZipCode,Email,PhoneNumber,ImageFile")] Contact contact, List<int> categoryList)
        {
            // remove AppUserId from validation check
            ModelState.Remove("AppUserId");

            if (ModelState.IsValid)
            {
                contact.AppUserId = _userManager.GetUserId(User);
                contact.Created = DateTime.UtcNow;

                if (contact.BirthDate != null)
                {
                    contact.BirthDate = DateTime.SpecifyKind(contact.BirthDate.Value, DateTimeKind.Utc);
                }

                // check whether a file/image has been selected
                // if ImageFile is NOT null, set ImageData property - convert file to byte[]
                // if ImageFile is NOT null, set ImageType property - use file extension as the value
                if (contact.ImageFile != null)
                {
                    contact.ImageData = await _imageService.ConvertFileToByteArrayAsync(contact.ImageFile);
                    contact.ImageType = contact.ImageFile.ContentType;
                }

                _context.Add(contact);
                await _context.SaveChangesAsync();

                // TODO: use the list of category Ids to...
                // 1. find associated Category
                // 2. add the category to the Collection for the current Contact
                foreach(int categoryId in categoryList)
                {
                    await _addressBookService.AddContactToCategoryAsync(categoryId, contact.Id);
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["StatesList"] = new SelectList(Enum.GetValues(typeof(States)).Cast<States>().ToList());
            string userId = _userManager.GetUserId(User);
            List<Category> categories = await _context.Categories
                                            .Where(c => c.AppUserId == userId)
                                            .ToListAsync();
            ViewData["CategoryList"] = new MultiSelectList(categories, "Id", "Name");

            return View(contact);
        }

        // GET: Contacts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Contacts == null)
            {
                return NotFound();
            }

            string appUserId = _userManager.GetUserId(User);

            // Contact? contact = await _context.Contacts.FindAsync(id);
            Contact? contact = await _context.Contacts
                                             .Include(c => c.Categories)
                                             .FirstOrDefaultAsync(c => c.Id == id && c.AppUserId == appUserId);

            if (contact == null)
            {
                return NotFound();
            }
            
            // load data for States dropdown
            ViewData["StatesList"] = new SelectList(Enum.GetValues(typeof(States)).Cast<States>().ToList());

            /* TODO: Categories drop down */
            // load data for custom Categories dropdown
            List<Category> categories = (await _addressBookService.GetAppUserCategoriesAsync(appUserId)).ToList();
            List<int> categoryIds = contact.Categories.Select(c => c.Id).ToList();

            ViewData["CategoryList"] = new MultiSelectList(categories, "Id", "Name", categoryIds);

            return View(contact);
        }

        // POST: Contacts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,AppUserId,FirstName,LastName,BirthDate,Address1,Address2,City,State,ZipCode,Email,PhoneNumber,Created,ImageFile,ImageData,ImageType")] Contact contact, List<int> categoryList)
        {
            if (id != contact.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    contact.Created = DateTime.SpecifyKind(contact.Created, DateTimeKind.Utc);

                    if (contact.BirthDate != null)
                    {
                        contact.BirthDate = DateTime.SpecifyKind(contact.BirthDate.Value, DateTimeKind.Utc);
                    }

                    // check whether a file/image has been selected
                    // if ImageFile is NOT null, set ImageData property - convert file to byte[]
                    // if ImageFile is NOT null, set ImageType property - use file extension as the value
                    if (contact.ImageFile != null)
                    {
                        contact.ImageData = await _imageService.ConvertFileToByteArrayAsync(contact.ImageFile);
                        contact.ImageType = contact.ImageFile.ContentType;
                    }

                    _context.Update(contact);
                    await _context.SaveChangesAsync();

                    // TODO: add Categories code

                    // remove current categories
                    await _addressBookService.RemoveAllContactCategoriesAsync(contact.Id);

                    // add selected categories to the contact
                    await _addressBookService.AddContactToCategoriesAsync(categoryList, contact.Id);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContactExists(contact.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            // load data for States dropdown
            ViewData["StatesList"] = new SelectList(Enum.GetValues(typeof(States)).Cast<States>().ToList());

            /* TODO: Categories drop down */
            // load data for custom Categories dropdown
            List<Category> categories = (await _addressBookService.GetAppUserCategoriesAsync(contact.AppUserId!)).ToList();
            List<int> categoryIds = contact.Categories.Select(c => c.Id).ToList();

            ViewData["CategoryList"] = new MultiSelectList(categories, "Id", "Name", categoryIds);

            return View(contact);
        }

        // GET: Contacts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Contacts == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts
                .Include(c => c.AppUser)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null)
            {
                return NotFound();
            }

            return View(contact);
        }

        // POST: Contacts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Contacts == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Contacts'  is null.");
            }
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ContactExists(int id)
        {
          return _context.Contacts.Any(e => e.Id == id);
        }
    }
}
