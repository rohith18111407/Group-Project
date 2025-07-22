import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, OnChanges, Output } from '@angular/core';
import { AdminService } from '../../services/admin.service';
import { StaffService } from '../../services/staff.service';
import { HttpClient } from '@angular/common/http';
import { AddItemComponent } from "../add-item/add-item";
import { AddUserComponent } from "../add-user/add-user";
import { EditUserComponent } from "../edit-user/edit-user";
import { EditItemComponent } from "../edit-item/edit-item";
import { DashboardComponent } from '../../dashboard/dashboard';
import { AddFileComponent } from "../add-file/add-file";
import { StatisticsComponent } from '../../statistics/statistics';

@Component({
  selector: 'app-center-content',
  imports: [CommonModule, AddItemComponent, AddUserComponent, EditUserComponent, EditItemComponent, DashboardComponent, AddFileComponent, StatisticsComponent],
  templateUrl: './center-content.html',
  styleUrl: './center-content.css'
})
export class CenterContentComponent implements OnChanges {
  @Input() view: 'dashboard' | 'files' | 'items' | 'users' | 'statistics' = 'dashboard';

  // Items
  items: any[] = [];
  loading = false;
  error: string | null = null;
  itemSearchTerm = '';
  filteredItems: any[] = [];
  showEditItemForm = false;
  selectedItemToEdit: any = null;
  sortBy: 'createdAt' | 'createdBy' | 'name' | 'category' = 'createdAt';
  isDescending = false;
  showAddItemForm = false;

  // Users
  users: any[] = [];
  filteredUsers: any[] = [];
  userSearchTerm = '';
  selectedRoleFilter = '';
  showEditUserForm = false;
  selectedUserToEdit: any = null;
  showAddUserForm = false;
  
  // User sorting properties for login information
  userSortBy: 'username' | 'lastlogin' | 'role' = 'username';
  userSortDescending = false;

  // Files
  files: any[] = [];
  fileSearchTerm = '';
  fileSortDescending = true;
  groupedFiles: { [category: string]: any[] } = {};
  showAddFileForm = false;

  // Archive functionality
  archivedFiles: any[] = [];
  showArchivedFiles = false;
  archiveLoading = false;
  archiveError: string | null = null;
  groupedArchivedFiles: { [category: string]: any[] } = {};
  currentUser: any = null;
  isAdmin = false;
  isStaff = false;

  constructor(
    private adminService: AdminService,
    private staffService: StaffService
  ) { 
    // Initialize user profile first
    this.loadUserProfile();
  }

  ngOnChanges(): void {
    if (this.view === 'items') {
      this.fetchItems();
    }
    if (this.view === 'users') {
      this.fetchUsers();
    }
    if (this.view === 'files') {
      this.fetchFiles();
    }
  }

  // Load user profile to determine role
  loadUserProfile(): void {
    // Try to get user info from session storage first
    const userJson = sessionStorage.getItem('user');
    const token = sessionStorage.getItem('jwtToken');
    
    if (userJson) {
      const user = JSON.parse(userJson);
      this.currentUser = user;
      this.isAdmin = user.roles?.includes('Admin') || false;
      this.isStaff = user.roles?.includes('Staff') || false;
      
      console.log('User loaded from session:', {
        userName: user.userName,
        username: user.username,
        roles: user.roles,
        isAdmin: this.isAdmin
      });
    } else if (token) {
      // Try to decode JWT token to get user info
      try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        console.log('JWT payload:', payload);
        
        const roles = payload.roles || payload.role || [];
        const username = payload.sub || payload.username || payload.name;
        
        this.currentUser = {
          userName: username,
          username: username,
          roles: Array.isArray(roles) ? roles : [roles]
        };
        
        this.isAdmin = this.currentUser.roles.includes('Admin');
        this.isStaff = this.currentUser.roles.includes('Staff');
        
        console.log('User loaded from JWT:', {
          userName: this.currentUser.userName,
          roles: this.currentUser.roles,
          isAdmin: this.isAdmin
        });
      } catch (e) {
        console.error('Failed to decode JWT:', e);
      }
    }

    // Always try to get fresh user data from API as well
    const service = this.isAdmin ? this.adminService : this.staffService;
    service.getUserProfile().subscribe({
      next: (user) => {
        this.currentUser = user;
        this.isAdmin = user.roles?.includes('Admin') || false;
        this.isStaff = user.roles?.includes('Staff') || false;
        
        console.log('User loaded from API:', {
          userName: user.userName,
          username: user.username,
          roles: user.roles,
          isAdmin: this.isAdmin
        });
      },
      error: (error) => {
        console.error('Failed to load user profile:', error);
      }
    });
  }

  // ITEMS METHODS
  fetchItems() {
    this.loading = true;
    this.adminService.getAllItems(this.sortBy, this.isDescending).subscribe({
      next: items => {
        this.items = items;
        this.applyItemFilters();
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to fetch items.';
        this.loading = false;
      }
    });
  }

  toggleAddItemForm(show: boolean) {
    this.showAddItemForm = show;
  }

  onItemCreated() {
    this.showAddItemForm = false;
    this.fetchItems();
  }

  confirmDelete(id: string) {
    const confirmed = confirm('Are you sure you want to delete this item?');
    if (confirmed) {
      this.adminService.deleteItem(id).subscribe({
        next: () => {
          alert('Item deleted successfully!');
          this.fetchItems();
        },
        error: () => alert('Failed to delete item.')
      });
    }
  }

  changeSort(field: 'createdAt' | 'createdBy' | 'name') {
    if (this.sortBy === field) {
      this.isDescending = !this.isDescending;
    } else {
      this.sortBy = field;
      this.isDescending = false;
    }
    this.fetchItems();
  }

  onItemSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.itemSearchTerm = input.value.trim().toLowerCase();
    this.applyItemFilters();
  }

  applyItemFilters() {
    this.filteredItems = this.items.filter(item => {
      const nameMatch = item.name?.toLowerCase().includes(this.itemSearchTerm);
      const categoryMatch = item.categories?.some((cat: string) =>
        cat.toLowerCase().includes(this.itemSearchTerm)
      );
      return nameMatch || categoryMatch;
    });
  }

  toggleEditItemForm(item: any) {
    this.selectedItemToEdit = item;
    this.showEditItemForm = true;
  }

  onItemUpdated() {
    this.showEditItemForm = false;
    this.fetchItems();
  }

  // USERS METHODS
  fetchUsers() {
    this.loading = true;
    this.error = null;

    this.adminService.getAllUsers().subscribe({
      next: res => {
        this.users = res;
        this.applyUserFilters();
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to fetch users.';
        this.loading = false;
      }
    });
  }

  applyUserFilters() {
    let filtered = this.users.filter(user => {
      const matchesUsername = user.username
        .toLowerCase()
        .includes(this.userSearchTerm.toLowerCase());

      const matchesRole = this.selectedRoleFilter
        ? user.roles?.includes(this.selectedRoleFilter)
        : true;

      return matchesUsername && matchesRole;
    });

    filtered = filtered.sort((a, b) => {
      let comparison = 0;
      
      switch (this.userSortBy) {
        case 'username':
          comparison = a.username.localeCompare(b.username);
          break;
        case 'lastlogin':
          const aTime = a.lastLoginAt ? new Date(a.lastLoginAt).getTime() : 0;
          const bTime = b.lastLoginAt ? new Date(b.lastLoginAt).getTime() : 0;
          comparison = aTime - bTime;
          break;
        case 'role':
          const aRole = a.roles?.[0] || '';
          const bRole = b.roles?.[0] || '';
          comparison = aRole.localeCompare(bRole);
          break;
      }

      return this.userSortDescending ? -comparison : comparison;
    });

    this.filteredUsers = filtered;
  }

  changeUserSort(field: 'username' | 'lastlogin' | 'role') {
    if (this.userSortBy === field) {
      this.userSortDescending = !this.userSortDescending;
    } else {
      this.userSortBy = field;
      this.userSortDescending = false;
    }
    this.applyUserFilters();
  }

  onUserSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.userSearchTerm = input.value.trim().toLowerCase();
    this.applyUserFilters();
  }

  onRoleFilterChange(event: Event): void {
    const selectElement = event.target as HTMLSelectElement;
    const role = selectElement.value;
    this.selectedRoleFilter = role;
    this.applyUserFilters();
  }

  toggleAddUserForm(show: boolean) {
    this.showAddUserForm = show;
  }

  onUserCreated() {
    this.showAddUserForm = false;
    this.fetchUsers();
  }

  confirmDeleteUser(id: string) {
    const confirmed = confirm('Are you sure you want to delete this user?');
    if (confirmed) {
      this.adminService.deleteUser(id).subscribe({
        next: () => {
          alert('User deleted successfully!');
          this.fetchUsers();
        },
        error: () => alert('Failed to delete user.')
      });
    }
  }

  toggleEditUserForm(user: any) {
    this.selectedUserToEdit = user;
    this.showEditUserForm = true;
  }

  onUserUpdated() {
    this.showEditUserForm = false;
    this.fetchUsers();
  }

  // FILES METHODS
  fetchFiles() {
    this.loading = true;
    this.error = null;

    const service = this.isAdmin ? this.adminService : this.staffService;
    service.getAllFiles().subscribe({
      next: res => {
        this.files = res;
        this.applyFileFilters();
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to fetch files.';
        this.loading = false;
      }
    });
  }

  onFileSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.fileSearchTerm = input.value.trim().toLowerCase();
    this.applyFileFilters();
    if (this.showArchivedFiles) {
      this.applyArchivedFileFilters();
    }
  }

  changeFileSortOrder(): void {
    this.fileSortDescending = !this.fileSortDescending;
    this.applyFileFilters();
    if (this.showArchivedFiles) {
      this.applyArchivedFileFilters();
    }
  }

  applyFileFilters(): void {
    const filtered = this.files.filter(file => {
      const nameMatch = file.fileName?.toLowerCase().includes(this.fileSearchTerm);
      const itemMatch = file.itemName?.toLowerCase().includes(this.fileSearchTerm);
      const categoryMatch = file.category?.toLowerCase().includes(this.fileSearchTerm);
      return nameMatch || itemMatch || categoryMatch;
    });

    const sorted = filtered.sort((a, b) => {
      const aTime = new Date(a.createdAt).getTime();
      const bTime = new Date(b.createdAt).getTime();
      return this.fileSortDescending ? bTime - aTime : aTime - bTime;
    });

    this.groupedFiles = sorted.reduce((acc: any, file: any) => {
      const category = file.category || 'Uncategorized';
      if (!acc[category]) acc[category] = [];
      acc[category].push(file);
      return acc;
    }, {});
  }

  getFormattedSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    else if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`;
    else return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  get groupedFileCategories(): string[] {
    return Object.keys(this.groupedFiles);
  }

  get groupedArchivedFileCategories(): string[] {
    return Object.keys(this.groupedArchivedFiles);
  }

  toggleAddFileForm(show: boolean) {
    this.showAddFileForm = show;
  }

  onFileUploaded() {
    this.showAddFileForm = false;
    this.fetchFiles();
  }

  confirmDeleteFile(id: string) {
    const confirmed = confirm('Are you sure you want to delete this file?');
    if (confirmed) {
      this.adminService.deleteFile(id).subscribe({
        next: (res) => {
          alert(res.message || 'File deleted successfully!');
          this.fetchFiles();
        },
        error: () => alert('Failed to delete file.')
      });
    }
  }

  onDownloadFile(file: any) {
    const service = this.isAdmin ? this.adminService : this.staffService;
    service.downloadFile(file.fileName, file.versionNumber).subscribe({
      next: (blob) => {
        const fileBlob = new Blob([blob], { type: 'application/octet-stream' });
        const url = window.URL.createObjectURL(fileBlob);

        const a = document.createElement('a');
        a.href = url;
        a.download = `${file.fileName}${file.fileExtension}`;
        a.click();
        window.URL.revokeObjectURL(url);

        alert('Download successful!');
      },
      error: () => {
        alert('Failed to download file.');
      }
    });
  }

  // ARCHIVE METHODS

  /**
   * Toggle showing archived files
   */
  toggleArchivedFiles(): void {
    this.showArchivedFiles = !this.showArchivedFiles;
    if (this.showArchivedFiles && this.archivedFiles.length === 0) {
      this.fetchArchivedFiles();
    }
  }

  /**
   * Fetch archived files
   */
  fetchArchivedFiles(): void {
    this.archiveLoading = true;
    this.archiveError = null;

    const service = this.isAdmin ? this.adminService : this.staffService;
    
    // If admin, get their own archived files, otherwise get all archived files
    // Handle both possible property names for username
    const currentUserName = this.currentUser?.userName || this.currentUser?.username;
    
    const request = this.isAdmin && currentUserName
      ? service.getArchivedFilesByAdmin(currentUserName)
      : service.getArchivedFiles();

    request.subscribe({
      next: (files) => {
        this.archivedFiles = files;
        console.log('Archived files loaded:', files.length);
        this.applyArchivedFileFilters();
        this.archiveLoading = false;
      },
      error: () => {
        this.archiveError = 'Failed to fetch archived files.';
        this.archiveLoading = false;
      }
    });
  }

  /**
   * Apply filters to archived files
   */
  applyArchivedFileFilters(): void {
    const filtered = this.archivedFiles.filter(file => {
      // Only show files archived within the last 2 days (temporarily visible)
      const archivedDate = new Date(file.archivedAt);
      const twoDaysAgo = new Date();
      twoDaysAgo.setDate(twoDaysAgo.getDate() - 2);
      
      const isRecentlyArchived = archivedDate >= twoDaysAgo;
      
      // For admins, show all their archived files regardless of date
      const isOwnFile = this.isAdmin && this.currentUser?.username === file.createdBy;
      
      if (!isRecentlyArchived && !isOwnFile) {
        return false;
      }

      // Apply search filter
      const nameMatch = file.fileName?.toLowerCase().includes(this.fileSearchTerm);
      const itemMatch = file.itemName?.toLowerCase().includes(this.fileSearchTerm);
      const categoryMatch = file.category?.toLowerCase().includes(this.fileSearchTerm);
      
      return nameMatch || itemMatch || categoryMatch;
    });

    const sorted = filtered.sort((a, b) => {
      const aTime = new Date(a.archivedAt).getTime();
      const bTime = new Date(b.archivedAt).getTime();
      return this.fileSortDescending ? bTime - aTime : aTime - bTime;
    });

    this.groupedArchivedFiles = sorted.reduce((acc: any, file: any) => {
      const category = file.category || 'Uncategorized';
      if (!acc[category]) acc[category] = [];
      acc[category].push(file);
      return acc;
    }, {});
  }

  /**
   * Check if a file was archived within the last 2 days
   */
  isRecentlyArchived(file: any): boolean {
    const archivedDate = new Date(file.archivedAt);
    const twoDaysAgo = new Date();
    twoDaysAgo.setDate(twoDaysAgo.getDate() - 2);
    return archivedDate >= twoDaysAgo;
  }

  /**
   * Check if current user can unarchive the file
   */
  canUnarchive(file: any): boolean {
    if (!this.isAdmin || !this.currentUser) {
      return false;
    }

    // Handle both possible property names for username
    const currentUserName = this.currentUser.userName || this.currentUser.username;
    const fileCreatedBy = file.createdBy;

    // Debug log to check the comparison
    console.log('Unarchive check:', {
      currentUserName,
      fileCreatedBy,
      isAdmin: this.isAdmin,
      canUnarchive: currentUserName === fileCreatedBy
    });

    return currentUserName === fileCreatedBy;
  }

  /**
   * Unarchive a file (Admin only)
   */
  confirmUnarchiveFile(file: any): void {
    if (!this.canUnarchive(file)) {
      alert('You can only unarchive your own files.');
      return;
    }

    const confirmed = confirm(`Are you sure you want to unarchive "${file.fileName}${file.fileExtension}"?`);
    if (confirmed) {
      this.adminService.unarchiveFile(file.id).subscribe({
        next: (response) => {
          alert(response.message || 'File unarchived successfully!');
          this.fetchArchivedFiles();
          this.fetchFiles(); // Refresh active files list
        },
        error: (error) => {
          alert('Failed to unarchive file.');
          console.error('Unarchive error:', error);
        }
      });
    }
  }

  /**
   * Bulk unarchive all files (Admin only)
   */
  confirmBulkUnarchiveFiles(): void {
    if (!this.isAdmin) {
      alert('Only admins can perform bulk unarchive operations.');
      return;
    }

    const filesToUnarchive = this.archivedFiles.filter(file => this.canUnarchive(file));
    
    if (filesToUnarchive.length === 0) {
      alert('No files available for unarchiving. You can only unarchive your own files.');
      return;
    }

    const confirmed = confirm(
      `Are you sure you want to unarchive ${filesToUnarchive.length} file(s)? This will restore them to active status.`
    );
    
    if (confirmed) {
      this.performBulkUnarchive(filesToUnarchive);
    }
  }

  /**
   * Perform bulk unarchive operation
   */
  private performBulkUnarchive(filesToUnarchive: any[]): void {
    let successCount = 0;
    let errorCount = 0;
    let processedCount = 0;

    const totalFiles = filesToUnarchive.length;

    filesToUnarchive.forEach(file => {
      this.adminService.unarchiveFile(file.id).subscribe({
        next: (response) => {
          successCount++;
          processedCount++;
          
          if (processedCount === totalFiles) {
            this.handleBulkUnarchiveComplete(successCount, errorCount);
          }
        },
        error: (error) => {
          errorCount++;
          processedCount++;
          console.error(`Failed to unarchive file ${file.fileName}:`, error);
          
          if (processedCount === totalFiles) {
            this.handleBulkUnarchiveComplete(successCount, errorCount);
          }
        }
      });
    });
  }

  /**
   * Handle bulk unarchive completion
   */
  private handleBulkUnarchiveComplete(successCount: number, errorCount: number): void {
    let message = '';
    
    if (successCount > 0 && errorCount === 0) {
      message = `Successfully unarchived ${successCount} file(s).`;
    } else if (successCount > 0 && errorCount > 0) {
      message = `Unarchived ${successCount} file(s) successfully, ${errorCount} failed.`;
    } else {
      message = `Failed to unarchive ${errorCount} file(s).`;
    }

    alert(message);
    
    // Refresh both lists
    this.fetchArchivedFiles();
    this.fetchFiles();
  }

  /**
   * Get count of files that can be unarchived by current admin
   */
  getUnarchivableFilesCount(): number {
    return this.archivedFiles.filter(file => this.canUnarchive(file)).length;
  }

  /**
   * Manually archive a file (Admin only)
   */
  confirmArchiveFile(file: any): void {
    if (!this.isAdmin) {
      alert('Only admins can archive files.');
      return;
    }

    const reason = prompt('Please provide a reason for archiving this file:');
    if (reason && reason.trim()) {
      this.adminService.archiveFileManually(file.id, reason.trim()).subscribe({
        next: (response) => {
          alert(response.message || 'File archived successfully!');
          this.fetchFiles(); // Refresh active files list
          if (this.showArchivedFiles) {
            this.fetchArchivedFiles(); // Refresh archived files list
          }
        },
        error: (error) => {
          alert('Failed to archive file.');
          console.error('Archive error:', error);
        }
      });
    }
  }

  /**
   * Get formatted date for display
   */
  getFormattedDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  /**
   * Get days since archived
   */
  getDaysSinceArchived(archivedAt: string): number {
    const archivedDate = new Date(archivedAt);
    const now = new Date();
    const diffTime = Math.abs(now.getTime() - archivedDate.getTime());
    return Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  }

  
}