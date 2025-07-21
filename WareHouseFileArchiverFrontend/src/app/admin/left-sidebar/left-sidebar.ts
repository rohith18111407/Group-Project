import { Component, EventEmitter, Output } from '@angular/core';

@Component({
  selector: 'app-left-sidebar',
  imports: [],
  templateUrl: './left-sidebar.html',
  styleUrl: './left-sidebar.css'
})
export class LeftSidebarComponent {
  @Output() viewChange = new EventEmitter<'dashboard' | 'files' | 'items' | 'users' | 'statistics' | 'scheduled'>();
  setView(view: 'dashboard' | 'files' | 'items' | 'users' | 'statistics' | 'scheduled') {
    this.viewChange.emit(view);
  }
  change(view: 'dashboard' | 'files' | 'items' | 'users' | 'statistics' | 'scheduled') {
    this.viewChange.emit(view);
  }
}
