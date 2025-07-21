import { Component, OnInit } from '@angular/core';
import { AdminService } from '../../services/admin.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-scheduled-uploads',
  imports: [CommonModule],
  templateUrl: './scheduled-uploads.html',
  styleUrl: './scheduled-uploads.css'
})
export class ScheduledUploadsComponent implements OnInit {
  scheduledUploads: any[] = [];
  loading = false;

  constructor(private adminService: AdminService) {}

  ngOnInit(): void {
    this.loadScheduledUploads();
  }

  loadScheduledUploads(): void {
    this.loading = true;
    this.adminService.getScheduledFiles().subscribe({
      next: (uploads) => {
        console.log('Scheduled uploads loaded:', uploads);
        this.scheduledUploads = uploads;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading scheduled uploads:', error);
        this.loading = false;
        alert('Failed to load scheduled uploads');
      }
    });
  }

  cancelScheduledUpload(id: string, fileName: string): void {
    if (confirm(`Are you sure you want to cancel the scheduled upload for "${fileName}"?`)) {
      this.adminService.cancelScheduledUpload(id).subscribe({
        next: () => {
          alert('Scheduled upload cancelled successfully');
          this.loadScheduledUploads();
        },
        error: (error) => {
          console.error('Error cancelling scheduled upload:', error);
          alert('Failed to cancel scheduled upload');
        }
      });
    }
  }

  getStatusClass(upload: any): string {
    if (upload.isProcessed) return 'badge bg-success';
    if (new Date(upload.createdAt) <= new Date()) return 'badge bg-warning';
    return 'badge bg-info';
  }

  getStatusText(upload: any): string {
    if (upload.isProcessed) return 'Processed';
    if (new Date(upload.createdAt) <= new Date()) return 'Due';
    return 'Scheduled';
  }

  formatDateTime(dateTime: string): string {
    return new Date(dateTime).toLocaleString();
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
