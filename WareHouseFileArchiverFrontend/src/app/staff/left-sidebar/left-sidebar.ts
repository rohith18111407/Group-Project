import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-staff-left-sidebar',
  imports: [RouterModule, CommonModule],
  templateUrl: './left-sidebar.html',
  styleUrl: './left-sidebar.css'
})
export class StaffLeftSidebarComponent {
  @Output() viewChange = new EventEmitter<'dashboard' | 'files' | 'items' | 'users' | 'statistics'>();
  setView(view: 'dashboard' | 'files' | 'items' | 'users' | 'statistics') {
    this.viewChange.emit(view);
  }
  change(view: 'dashboard' | 'files' | 'items' | 'users' | 'statistics') {
    this.viewChange.emit(view);
  }
}
