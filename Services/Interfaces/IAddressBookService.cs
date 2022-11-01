using ContactMVC.Models;

namespace ContactMVC.Services.Interfaces
{
    public interface IAddressBookService
    {
        public Task AddContactToCategoryAsync(int categoryId, int contactId);

        // add method to add to a list of CategoryIds
        public Task AddContactToCategoriesAsync(IEnumerable<int> categoryIds, int contactId);

        public Task<bool> IsContactinCategory(int categoryId, int contactId);

        public Task<IEnumerable<Category>> GetAppUserCategoriesAsync(string appUserId);

        // add method to remove from all Categories
        public Task RemoveAllContactCategoriesAsync(int contactId);
    }
}
