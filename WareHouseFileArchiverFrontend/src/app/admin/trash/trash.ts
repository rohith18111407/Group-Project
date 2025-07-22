import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../services/admin.service';
import { TrashFile, TrashStats } from '../../models/trash.model';

@Component({
  selector: 'app-trash',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './trash.html',
  styleUrls: ['./trash.css']
})
export class TrashComponent implements OnInit {
  trashedFiles: TrashFile[] = [];
  filteredTrashedFiles: TrashFile[] = [];
  trashStats: TrashStats | null = null;
  loading = false;
  error = '';
  searchTerm = '';
  sortBy: 'fileName' | 'deletedAt' | 'deletedBy' | 'daysRemaining' = 'deletedAt';
  isDescending = true;
  showConfirmDialog = false;
  confirmAction: 'restore' | 'permanentDelete' | 'cleanup' = 'restore';
  selectedFile: TrashFile | null = null;

  constructor(private adminService: AdminService) {}

  ngOnInit() {
    this.loadTrashedFiles();
    this.loadTrashStats();
  }

  loadTrashedFiles() {
    this.loading = true;
    this.error = '';
    
    this.adminService.getTrashedFiles().subscribe({
      next: (files) => {
        this.trashedFiles = files;
        this.filterAndSortFiles();
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load trashed files. Please try again.';
        this.loading = false;
        console.error('Error loading trashed files:', err);
      }
    });
  }

  loadTrashStats() {
    this.adminService.getTrashStats().subscribe({
      next: (stats) => {
        this.trashStats = stats;
      },
      error: (err) => {
        console.error('Error loading trash stats:', err);
      }
    });
  }

  onSearchChange(event: any) {
    this.searchTerm = event.target.value.toLowerCase();
    this.filterAndSortFiles();
  }

  changeSort(field: 'fileName' | 'deletedAt' | 'deletedBy' | 'daysRemaining') {
    if (this.sortBy === field) {
      this.isDescending = !this.isDescending;
    } else {
      this.sortBy = field;
      this.isDescending = true;
    }
    this.filterAndSortFiles();
  }

  filterAndSortFiles() {
    // Filter files based on search term
    let filtered = this.trashedFiles.filter(file => 
      file.fileName.toLowerCase().includes(this.searchTerm) ||
      file.itemName?.toLowerCase().includes(this.searchTerm) ||
      file.deletedBy.toLowerCase().includes(this.searchTerm)
    );

    // Sort files
    filtered.sort((a, b) => {
      let comparison = 0;
      
      switch (this.sortBy) {
        case 'fileName':
          comparison = a.fileName.localeCompare(b.fileName);
          break;
        case 'deletedAt':
          comparison = new Date(a.deletedAt).getTime() - new Date(b.deletedAt).getTime();
          break;
        case 'deletedBy':
          comparison = a.deletedBy.localeCompare(b.deletedBy);
          break;
        case 'daysRemaining':
          comparison = a.daysRemaining - b.daysRemaining;
          break;
      }
      
      return this.isDescending ? -comparison : comparison;
    });

    this.filteredTrashedFiles = filtered;
  }

  confirmRestore(file: TrashFile) {
    this.selectedFile = file;
    this.confirmAction = 'restore';
    this.showConfirmDialog = true;
  }

  confirmPermanentDelete(file: TrashFile) {
    this.selectedFile = file;
    this.confirmAction = 'permanentDelete';
    this.showConfirmDialog = true;
  }

  confirmCleanup() {
    this.selectedFile = null;
    this.confirmAction = 'cleanup';
    this.showConfirmDialog = true;
  }

  executeAction() {
    if (!this.selectedFile && this.confirmAction !== 'cleanup') return;

    this.loading = true;
    
    switch (this.confirmAction) {
      case 'restore':
        this.adminService.restoreFromTrash(this.selectedFile!.id).subscribe({
          next: () => {
            this.loadTrashedFiles();
            this.loadTrashStats();
            this.showConfirmDialog = false;
          },
          error: (err) => {
            this.error = 'Failed to restore file. Please try again.';
            this.loading = false;
            console.error('Error restoring file:', err);
          }
        });
        break;
        
      case 'permanentDelete':
        this.adminService.permanentlyDeleteFromTrash(this.selectedFile!.id).subscribe({
          next: () => {
            this.loadTrashedFiles();
            this.loadTrashStats();
            this.showConfirmDialog = false;
          },
          error: (err) => {
            this.error = 'Failed to permanently delete file. Please try again.';
            this.loading = false;
            console.error('Error permanently deleting file:', err);
          }
        });
        break;
        
      case 'cleanup':
        this.adminService.forceCleanupTrash().subscribe({
          next: () => {
            this.loadTrashedFiles();
            this.loadTrashStats();
            this.showConfirmDialog = false;
          },
          error: (err) => {
            this.error = 'Failed to cleanup trash. Please try again.';
            this.loading = false;
            console.error('Error cleaning up trash:', err);
          }
        });
        break;
    }
  }

  cancelAction() {
    this.showConfirmDialog = false;
    this.selectedFile = null;
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString() + ' ' + new Date(dateString).toLocaleTimeString();
  }

  getDaysRemainingClass(daysRemaining: number): string {
    if (daysRemaining <= 1) return 'text-danger fw-bold';
    if (daysRemaining <= 3) return 'text-warning fw-bold';
    return 'text-success';
  }

  trackByFileId(index: number, file: TrashFile): string {
    return file.id;
  }
}
