# SVN2GitHub WPF Application - Test Plan

## Test Coverage Overview

### 1. Main Window Navigation
- [x] Toggle menu button
- [ ] "DASHBOARD" button → switches to Dashboard view
- [ ] "NEW MIGRATION" button → switches to New Migration view

### 2. Dashboard View
- [ ] Refresh button → loads migrations from API
- [ ] "View Details" button → shows migration details (for each migration item)
- [ ] Empty state display (when no migrations)
- [ ] Migration list display (when migrations exist)

### 3. New Migration Form
- [ ] SVN URL input validation
- [ ] GitHub Organization input validation
- [ ] GitHub Repository input validation
- [ ] GitHub Token input validation
- [ ] "Configure Authors" button → opens Author Mapping window
- [ ] "CLEAR" button → clears all form fields
- [ ] "START MIGRATION" button → creates migration and sends to API
- [ ] Form validation (required fields)

### 4. Author Mapping Window
- [ ] "Load from SVN" button → loads authors from SVN repo
- [ ] "Add Author" button → opens Add Author dialog
- [ ] Delete author button (per row)
- [ ] Edit author inline (grid editing)
- [ ] "Export" button → saves author mapping to file
- [ ] "SAVE" button → saves and closes window

### 5. Add Author Dialog
- [ ] SVN Username input
- [ ] Git Name input
- [ ] Git Email input with validation
- [ ] "CANCEL" button → closes without saving
- [ ] "ADD" button → adds author and closes

### 6. API Integration
- [ ] GET /api/migrations (Dashboard refresh)
- [ ] POST /api/migrations (Start new migration)
- [ ] GET /api/migrations/{id} (View details)
- [ ] GET /api/authors (Load authors)
- [ ] POST /api/authors (Save authors)

### 7. Error Handling
- [ ] API connection failure
- [ ] Invalid form input
- [ ] Network timeout
- [ ] Invalid SVN URL
- [ ] Invalid email format

### 8. Visual States
- [ ] Loading indicators
- [ ] Success messages
- [ ] Error messages
- [ ] Empty states
- [ ] Disabled states (buttons during operation)

## Test Execution Status
- Total Tests: 35
- Passed: 1
- Failed: 0
- Pending: 34
