import { Component, OnInit, OnDestroy } from '@angular/core';
import { Observable } from 'rxjs';
import { NotificationService, Notification } from '../../services/notification.service';
import { CommonModule, DatePipe } from '@angular/common';
import { StaffService } from '../../services/staff.service';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-staff-right-sidebar',
  imports: [CommonModule, DatePipe],
  templateUrl: './right-sidebar.html',
  styleUrl: './right-sidebar.css'
})
export class StaffRightSidebarComponent implements OnInit, OnDestroy {
  user: any = null;
  loading = true;
  error: string | null = null;

  unreadCount$: Observable<number>;
  notifications$: Observable<Notification[]>;
  connectionState$: Observable<boolean>;
  showNotifications = false;

  constructor(
    private staffService: StaffService,
    private auth: AuthService,
    private router: Router,
    private notificationService: NotificationService
  ) {
    this.unreadCount$ = this.notificationService.unreadCount$;
    this.notifications$ = this.notificationService.notifications$;
    this.connectionState$ = this.notificationService.connectionState$;
  }

  ngOnInit(): void {
    this.loadUserProfile();
  }

  ngOnDestroy(): void {
    this.notificationService.disconnect();
  }

  private loadUserProfile(): void {
    this.staffService.getUserProfile().subscribe({
      next: (data) => {
        this.user = data;
        this.loading = false;
        console.log('Staff user profile loaded:', data);
      },
      error: (error) => {
        console.error('Error loading user profile:', error);
        this.error = 'Failed to load user info';
        this.loading = false;
      }
    });
  }


  toggleNotifications(): void {
    this.showNotifications = !this.showNotifications;
  }

  markAsRead(notificationId: string): void {
    this.notificationService.markAsRead(notificationId);
  }

  markAllAsRead(): void {
    this.notificationService.markAllAsRead();
  }

  clearNotification(notificationId: string): void {
    this.notificationService.clearNotification(notificationId);
  }

  clearNotifications(): void {
    const confirmed = confirm('Are you sure you want to clear all notifications?');
    if (confirmed) {
      this.notificationService.clearAllNotifications();
    }
  }

  getRelativeTime(timestamp: Date): string {
    const now = new Date();
    const time = new Date(timestamp);
    const diff = now.getTime() - time.getTime();
    
    const minutes = Math.floor(diff / (1000 * 60));
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));

    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;
    if (hours < 24) return `${hours}h ago`;
    if (days < 7) return `${days}d ago`;
    
    return time.toLocaleDateString();
  }

  getNotificationIcon(type: string): string {
    switch (type) {
      case 'success': return '✅';
      case 'warning': return '⚠️';
      case 'error': return '❌';
      case 'info': default: return 'ℹ️';
    }
  }

  getNotificationClass(type: string): string {
    switch (type) {
      case 'success': return 'notification-success';
      case 'warning': return 'notification-warning';
      case 'error': return 'notification-error';
      case 'info': default: return 'notification-info';
    }
  }

  logout(): void {
    const confirmed = confirm('Are you sure you want to logout?');
    if (confirmed) {
      this.notificationService.disconnect();
      this.auth.logout();
      this.router.navigate(['/login']);
    }
  }
}