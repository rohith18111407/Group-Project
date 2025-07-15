import { Component, computed, OnInit } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs';

@Component({
  selector: 'app-header',
  imports: [RouterModule, CommonModule],
  templateUrl: './header.html',
  styleUrl: './header.css'
})
export class HeaderComponent implements OnInit {
  isLoggedIn = false;
  currentPath = '';

  constructor(private auth: AuthService, private router: Router) { }

  ngOnInit(): void {
    // Set initial login state and path
    this.isLoggedIn = this.auth.isLoggedIn();
    this.currentPath = this.router.url;

    // Listen to route changes
    this.router.events.pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: any) => {
        this.currentPath = event.url;
        this.isLoggedIn = this.auth.isLoggedIn(); // Refresh login status
      });
  }

  inProtectedDashboard(): boolean {
    return this.isLoggedIn && (this.currentPath.startsWith('/admin') || this.currentPath.startsWith('/staff'));
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  isInAdmin(): boolean {
    return this.isLoggedIn && this.currentPath.startsWith('/admin');
  }

}
