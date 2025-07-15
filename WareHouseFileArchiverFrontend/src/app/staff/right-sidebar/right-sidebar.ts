import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { NotificationService } from '../../services/notification.service';
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
export class StaffRightSidebarComponent implements OnInit {
  user: any = null;
  loading = true;
  error: string | null = null;

  unreadCount$: Observable<number>;
  notifications$: Observable<any[]>;
  showNotifications = false;

  constructor(
    private staffService: StaffService,
    private auth: AuthService,
    private router: Router,
    private notificationService: NotificationService
  ) {
    this.unreadCount$ = this.notificationService.unreadCount$;
    this.notifications$ = this.notificationService.notifications$;
  }

  ngOnInit(): void {
    this.staffService.getUserProfile().subscribe({
      next: (data) => {
        this.user = data;
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load user info';
        this.loading = false;
      }
    });
  }

  toggleNotifications(): void {
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications) {
      this.notificationService.markAllAsRead();
    }
  }

  logout(): void {
    const confirmed = confirm('Are you sure you want to logout?');
    if (confirmed) {
      this.auth.logout();
      this.router.navigate(['/login']);
    }
  }

  clearNotifications(): void {
    const confirmed = confirm('Are you sure you want to clear all notifications?');
    if (confirmed) {
      this.notificationService.clearAllNotifications();
    }
  }

}
