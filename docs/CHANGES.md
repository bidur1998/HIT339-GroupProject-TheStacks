# Change log: Assignment 2 baseline → Group Project

Every file added or changed on top of my Assignment 2 code, grouped by feature. It's useful for planning commits and for the individual-contribution evidence table.

## Files changed from Assignment 2 (shared by several features)

- `LibraryManagementSystem/Controllers/BorrowersController.cs`
- `LibraryManagementSystem/Controllers/HomeController.cs`
- `LibraryManagementSystem/Controllers/ItemsController.cs`
- `LibraryManagementSystem/Controllers/ManagerController.cs`
- `LibraryManagementSystem/Controllers/ReceptionController.cs`
- `LibraryManagementSystem/Controllers/SearchController.cs`
- `LibraryManagementSystem/Data/ApplicationDbContext.cs`
- `LibraryManagementSystem/Data/DbInitializer.cs`
- `LibraryManagementSystem/LibraryManagementSystem.csproj`
- `LibraryManagementSystem/Models/Borrower.cs`
- `LibraryManagementSystem/Models/Item.cs`
- `LibraryManagementSystem/Models/Loan.cs`
- `LibraryManagementSystem/Models/ViewModels/ItemFormViewModel.cs`
- `LibraryManagementSystem/Models/ViewModels/SearchViewModel.cs`
- `LibraryManagementSystem/Program.cs`
- `LibraryManagementSystem/Properties/launchSettings.json`
- `LibraryManagementSystem/Repositories/BorrowerRepository.cs`
- `LibraryManagementSystem/Repositories/IBorrowerRepository.cs`
- `LibraryManagementSystem/Repositories/IItemRepository.cs`
- `LibraryManagementSystem/Repositories/ILoanRepository.cs`
- `LibraryManagementSystem/Repositories/ItemRepository.cs`
- `LibraryManagementSystem/Repositories/LoanRepository.cs`
- `LibraryManagementSystem/Services/ILoanService.cs`
- `LibraryManagementSystem/Services/LoanService.cs`
- `LibraryManagementSystem/Views/Borrowers/Create.cshtml`
- `LibraryManagementSystem/Views/Borrowers/Details.cshtml`
- `LibraryManagementSystem/Views/Borrowers/Edit.cshtml`
- `LibraryManagementSystem/Views/Borrowers/Index.cshtml`
- `LibraryManagementSystem/Views/Home/Index.cshtml`
- `LibraryManagementSystem/Views/Items/Delete.cshtml`
- `LibraryManagementSystem/Views/Items/Details.cshtml`
- `LibraryManagementSystem/Views/Items/Index.cshtml`
- `LibraryManagementSystem/Views/Items/_ItemForm.cshtml`
- `LibraryManagementSystem/Views/Manager/Dashboard.cshtml`
- `LibraryManagementSystem/Views/Reception/Borrow.cshtml`
- `LibraryManagementSystem/Views/Reception/Return.cshtml`
- `LibraryManagementSystem/Views/Reception/ReturnResult.cshtml`
- `LibraryManagementSystem/Views/Search/Index.cshtml`
- `LibraryManagementSystem/Views/Shared/_Layout.cshtml`
- `LibraryManagementSystem/Views/_ViewImports.cshtml`
- `LibraryManagementSystem/appsettings.json`
- `LibraryManagementSystem/wwwroot/css/site.css`
- moved: `LibraryManagementSystem/.gitignore -> .gitignore`
- moved: `LibraryManagementSystem/README.md -> docs/Assignment2-README.md`
- removed: `LibraryManagementSystem/LibraryManagementSystem.slnx` (the solution file now sits at the repository root)

## New: Upgrade to .NET 10 / solution

- `LibraryManagementSystem.slnx`
- `README.md`
- `docs/sample-import.csv`

## New: Multi-branch inventory and transfers

- `LibraryManagementSystem/Controllers/BranchesController.cs`
- `LibraryManagementSystem/Controllers/DeskBranchFilter.cs`
- `LibraryManagementSystem/Controllers/DeskController.cs`
- `LibraryManagementSystem/Controllers/TransfersController.cs`
- `LibraryManagementSystem/Models/Branch.cs`
- `LibraryManagementSystem/Models/BranchTransfer.cs`
- `LibraryManagementSystem/Repositories/BranchRepository.cs`
- `LibraryManagementSystem/Repositories/IBranchRepository.cs`
- `LibraryManagementSystem/Repositories/ITransferRepository.cs`
- `LibraryManagementSystem/Repositories/TransferRepository.cs`
- `LibraryManagementSystem/Services/BranchContext.cs`
- `LibraryManagementSystem/Services/TransferService.cs`
- `LibraryManagementSystem/Views/Branches/Create.cshtml`
- `LibraryManagementSystem/Views/Branches/Edit.cshtml`
- `LibraryManagementSystem/Views/Branches/Index.cshtml`
- `LibraryManagementSystem/Views/Branches/_BranchForm.cshtml`
- `LibraryManagementSystem/Views/Desk/Index.cshtml`
- `LibraryManagementSystem/Views/Transfers/Index.cshtml`
- `LibraryManagementSystem/Views/Transfers/_TransferTable.cshtml`

## New: Notifications

- `LibraryManagementSystem.Tests/BranchTransferNotificationTests.cs`
- `LibraryManagementSystem/Controllers/NotificationsController.cs`
- `LibraryManagementSystem/Models/NotificationLog.cs`
- `LibraryManagementSystem/Repositories/INotificationRepository.cs`
- `LibraryManagementSystem/Repositories/NotificationRepository.cs`
- `LibraryManagementSystem/Services/INotificationService.cs`
- `LibraryManagementSystem/Services/NotificationService.cs`
- `LibraryManagementSystem/Services/NotificationTemplates.cs`
- `LibraryManagementSystem/Services/ReminderService.cs`
- `LibraryManagementSystem/Views/Notifications/Index.cshtml`

## New: Reservations / waitlist

- `LibraryManagementSystem.Tests/ReservationServiceTests.cs`
- `LibraryManagementSystem/Controllers/ReservationsController.cs`
- `LibraryManagementSystem/Models/Reservation.cs`
- `LibraryManagementSystem/Repositories/IReservationRepository.cs`
- `LibraryManagementSystem/Repositories/ReservationRepository.cs`
- `LibraryManagementSystem/Services/IReservationService.cs`
- `LibraryManagementSystem/Services/ReservationService.cs`
- `LibraryManagementSystem/Views/Reservations/Confirmation.cshtml`
- `LibraryManagementSystem/Views/Reservations/Index.cshtml`
- `LibraryManagementSystem/Views/Reservations/Place.cshtml`
- `LibraryManagementSystem/Views/Reservations/_CancelHold.cshtml`

## New: Kiosk and patron PIN

- `LibraryManagementSystem/Controllers/KioskController.cs`
- `LibraryManagementSystem/Services/PatronAccountService.cs`
- `LibraryManagementSystem/Services/PatronAuthService.cs`
- `LibraryManagementSystem/Views/Kiosk/Account.cshtml`
- `LibraryManagementSystem/Views/Kiosk/Checkout.cshtml`
- `LibraryManagementSystem/Views/Kiosk/Index.cshtml`
- `LibraryManagementSystem/Views/Kiosk/Menu.cshtml`
- `LibraryManagementSystem/Views/Kiosk/Pin.cshtml`
- `LibraryManagementSystem/Views/Kiosk/_Keypad.cshtml`
- `LibraryManagementSystem/Views/Shared/_KioskLayout.cshtml`
- `LibraryManagementSystem/wwwroot/css/kiosk.css`
- `LibraryManagementSystem/wwwroot/js/kiosk.js`

## New: Public REST API

- `LibraryManagementSystem/Api/ApiDtos.cs`
- `LibraryManagementSystem/Api/ApiKeyAuthorization.cs`
- `LibraryManagementSystem/Api/CatalogueApiController.cs`
- `LibraryManagementSystem/LibraryManagementSystem.http`
- `LibraryManagementSystem/Views/Home/Developers.cshtml`

## New: Importer

- `LibraryManagementSystem.Tests/ImportAndExportTests.cs`
- `LibraryManagementSystem/Controllers/ImportController.cs`
- `LibraryManagementSystem/Services/CatalogueImportService.cs`
- `LibraryManagementSystem/Services/CsvParser.cs`
- `LibraryManagementSystem/Services/MediaMetadataProvider.cs`
- `LibraryManagementSystem/Views/Import/Index.cshtml`

## New: Reports and export

- `LibraryManagementSystem/Services/Reports/CsvReportExporter.cs`
- `LibraryManagementSystem/Services/Reports/PdfReportExporter.cs`
- `LibraryManagementSystem/Services/Reports/Report.cs`
- `LibraryManagementSystem/Services/Reports/ReportService.cs`
- `LibraryManagementSystem/Services/Reports/SimplePdfDocument.cs`
- `LibraryManagementSystem/Views/Manager/Reports.cshtml`

## New: Unit tests

- `LibraryManagementSystem.Tests/LibraryManagementSystem.Tests.csproj`
- `LibraryManagementSystem.Tests/LoanServiceTests.cs`
- `LibraryManagementSystem.Tests/TestSupport/InMemoryLibrary.cs`

## New: shared / supporting

- `LibraryManagementSystem/Models/ViewModels/GroupProjectViewModels.cs`
- `LibraryManagementSystem/Services/Settings.cs`
- `LibraryManagementSystem/Views/Borrowers/_BorrowerForm.cshtml`
