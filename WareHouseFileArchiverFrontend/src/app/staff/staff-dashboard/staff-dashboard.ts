import { Component, inject } from '@angular/core';
import { StaffLeftSidebarComponent } from "../left-sidebar/left-sidebar";
import { StaffCenterContentComponent } from "../center-content/center-content";
import { StaffRightSidebarComponent } from "../right-sidebar/right-sidebar";
import { StaffService } from '../../services/staff.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-staff-dashboard',
  imports: [StaffLeftSidebarComponent, StaffCenterContentComponent, StaffRightSidebarComponent,CommonModule],
  templateUrl: './staff-dashboard.html',
  styleUrl: './staff-dashboard.css'
})
export class StaffDashboardComponent {
  selectedView: 'dashboard' | 'files' | 'items' | 'users' | 'statistics' = 'dashboard';
  adminService = inject(StaffService);

  setView(view: 'dashboard' | 'files' | 'items' | 'users' | 'statistics') {
    this.selectedView = view;
  }
}
