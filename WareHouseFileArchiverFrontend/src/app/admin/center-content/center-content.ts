import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, OnChanges, Output } from '@angular/core';
import { AdminService } from '../../services/admin.service';
import { HttpClient } from '@angular/common/http';
import { AddItemComponent } from "../add-item/add-item";
import { AddUserComponent } from "../add-user/add-user";
import { EditUserComponent } from "../edit-user/edit-user";
import { EditItemComponent } from "../edit-item/edit-item";
import { DashboardComponent } from '../../dashboard/dashboard';
import { AddFileComponent } from "../add-file/add-file";
import { StatisticsComponent } from '../../statistics/statistics';
import { ScheduledUploadsComponent } from "../scheduled-uploads/scheduled-uploads";

@Component({
  selector: 'app-center-content',
  imports: [CommonModule, AddItemComponent, AddUserComponent, EditUserComponent, EditItemComponent, DashboardComponent, AddFileComponent, StatisticsComponent, ScheduledUploadsComponent],
  templateUrl: './center-content.html',
  styleUrl: './center-content.css'
})
export class CenterContentComponent implements OnChanges {
  @Input() view: 'dashboard' | 'files' | 'items' | 'users' | 'statistics' | 'scheduled' = 'dashboard';

  items: any[] = [];
  loading = false;
  error: string | null = null;
  itemSearchTerm = '';
  filteredItems: any[] = [];

  showEditItemForm = false;
  selectedItemToEdit: any = null;

  users: any[] = [];
  filteredUsers: any[] = [];

  userSearchTerm = '';
  selectedRoleFilter = '';

  showEditUserForm = false;
  selectedUserToEdit: any = null;


  sortBy: 'createdAt' | 'createdBy' | 'name' | 'category' = 'createdAt';
  isDescending = false;

  showAddItemForm = false;

  // Files
  files: any[] = [];
  fileSearchTerm = '';
  fileSortDescending = true;
  groupedFiles: { [category: string]: any[] } = {};

  showAddFileForm = false;

  constructor(private adminService: AdminService) { }

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

  fetchItems() {
    this.loading = true;
    this.adminService.getAllItems(this.sortBy, this.isDescending).subscribe({
      next: items => {
        this.items = items;
        this.applyItemFilters(); // filter based on search input
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

  fetchUsers() {
    this.loading = true;
    this.error = null;

    this.adminService.getAllUsers().subscribe({
      next: res => {
        this.users = res;
        this.applyUserFilters(); // <- filter after fetch
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to fetch users.';
        this.loading = false;
      }
    });
  }

  applyUserFilters() {
    this.filteredUsers = this.users.filter(user => {
      const matchesUsername = user.username
        .toLowerCase()
        .includes(this.userSearchTerm.toLowerCase());

      const matchesRole = this.selectedRoleFilter
        ? user.roles?.includes(this.selectedRoleFilter)
        : true;

      return matchesUsername && matchesRole;
    });
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

  showAddUserForm = false;

  toggleAddUserForm(show: boolean) {
    this.showAddUserForm = show;
  }

  onUserCreated() {
    this.showAddUserForm = false;
    this.fetchUsers(); // refresh after user is added
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
    this.fetchUsers(); // Refresh user list
  }

  fetchFiles() {
    this.loading = true;
    this.error = null;

    this.adminService.getAllFiles().subscribe({
      next: res => {
        // set only processed files
        this.files = res;
        this.applyFileFilters(); // Apply filters and group
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
  }

  changeFileSortOrder(): void {
    this.fileSortDescending = !this.fileSortDescending;
    this.applyFileFilters();
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
    this.adminService.downloadFile(file.fileName, file.versionNumber).subscribe({
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

}
