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

  // Archive functionality (read-only for staff)
  archivedFiles: any[] = [];
  showArchivedFiles = false;
  archiveLoading = false;
  archiveError: string | null = null;
  groupedArchivedFiles: { [category: string]: any[] } = {};

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
        this.applyItemFilters();
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

  // ARCHIVE METHODS (Staff - Read Only)

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
   * Fetch archived files (Staff can only view)
   */
  fetchArchivedFiles(): void {
    this.archiveLoading = true;
    this.archiveError = null;

    this.staffService.getArchivedFiles().subscribe({
      next: (files) => {
        this.archivedFiles = files;
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
   * Apply filters to archived files (Staff only sees recently archived files)
   */
  applyArchivedFileFilters(): void {
    const filtered = this.archivedFiles.filter(file => {
      // Staff can only see files archived within the last 2 days
      const archivedDate = new Date(file.archivedAt);
      const twoDaysAgo = new Date();
      twoDaysAgo.setDate(twoDaysAgo.getDate() - 2);
      
      const isRecentlyArchived = archivedDate >= twoDaysAgo;
      
      if (!isRecentlyArchived) {
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