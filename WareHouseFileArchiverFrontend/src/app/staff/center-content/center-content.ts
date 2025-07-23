import { Component, Input, OnChanges } from '@angular/core';
import { StaffService } from '../../services/staff.service';
import { CommonModule } from '@angular/common';
import { DashboardComponent } from "../../dashboard/dashboard";
import { StatisticsComponent } from '../../statistics/statistics';

@Component({
  selector: 'app-staff-center-content',
  imports: [CommonModule, DashboardComponent, StatisticsComponent],
  templateUrl: './center-content.html',
  styleUrl: './center-content.css'
})
export class StaffCenterContentComponent implements OnChanges {
  @Input() view: 'dashboard' | 'files' | 'items' | 'users' | 'statistics' = 'dashboard';

  items: any[] = [];
  loading = false;
  error: string | null = null;
  itemSearchTerm = '';
  filteredItems: any[] = [];

  users: any[] = [];
  filteredUsers: any[] = [];

  userSearchTerm = '';
  selectedRoleFilter = '';

  sortBy: 'createdAt' | 'createdBy' | 'name' | 'category' = 'createdAt';
  isDescending = false;

  // Files
  files: any[] = [];
  fileSearchTerm = '';
  fileSortDescending = true;
  groupedFiles: { [category: string]: any[] } = {};


  // Bulk selection properties
  selectedFiles = new Set<string>();
  showBulkActions = false;
  allFilesSelected = false;

  constructor(private staffService: StaffService) { }

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
    this.staffService.getAllItems(this.sortBy, this.isDescending).subscribe({
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


  fetchUsers() {
    this.loading = true;
    this.error = null;

    this.staffService.getAllUsers().subscribe({
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

  fetchFiles() {
    this.loading = true;
    this.error = null;

    this.staffService.getAllFiles().subscribe({
      next: res => {
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


  onDownloadFile(file: any) {
    this.staffService.downloadFile(file.fileName, file.versionNumber).subscribe({
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

  get totalFileCount(): number {
    return this.files.length;
  }

  isFileSelected(fileId: string): boolean {
    return this.selectedFiles.has(fileId);
  }

  toggleFileSelection(fileId: string, event: any): void {
    if (event.target.checked) {
      this.selectedFiles.add(fileId);
    } else {
      this.selectedFiles.delete(fileId);
    }
    this.updateBulkActionsVisibility();
    this.updateSelectAllState();
  }

  selectAllFiles(event: any): void {
    if (event.target.checked) {
      // Select all files
      this.files.forEach(file => this.selectedFiles.add(file.id));
      this.allFilesSelected = true;
    } else {
      // Deselect all files
      this.selectedFiles.clear();
      this.allFilesSelected = false;
    }
    this.updateBulkActionsVisibility();
  }

  updateBulkActionsVisibility(): void {
    this.showBulkActions = this.selectedFiles.size > 0;
  }

  updateSelectAllState(): void {
    this.allFilesSelected = this.files.length > 0 && this.selectedFiles.size === this.files.length;
  }

  cancelBulkSelection(): void {
    this.selectedFiles.clear();
    this.showBulkActions = false;
    this.allFilesSelected = false;
  }

  bulkDownloadFiles(): void {
    if (this.selectedFiles.size === 0) {
      alert('Please select files to download.');
      return;
    }

    const selectedFileIds = Array.from(this.selectedFiles);
    
    this.staffService.bulkDownloadFiles(selectedFileIds).subscribe({
      next: (blob: Blob) => {
        const fileBlob = new Blob([blob], { type: 'application/zip' });
        const url = window.URL.createObjectURL(fileBlob);

        const a = document.createElement('a');
        a.href = url;
        a.download = `ArchiveFiles_${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.zip`;
        a.click();
        window.URL.revokeObjectURL(url);

        alert(`Successfully downloaded ${this.selectedFiles.size} files as ZIP.`);
        this.cancelBulkSelection();
      },
      error: () => {
        alert('Failed to download files. Please try again.');
      }
    });
  }

}
